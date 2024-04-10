using ConsoleApp3.ReflectionStatement;
using RussianBISqlOptimizer.Statements;
using System.Net.Http.Headers;
using System.Reflection;

namespace RussianBISqlOptimizer;

public static class SqlOptimizer
{
    public static SelectQuery Optimize(SelectQuery baseQuery)
    {
        var query = baseQuery.ReplaceFrom();

        var dict = CalcColumnNameReplaceDict(query?.GetSelectStatements());

        var whereCondition = query?.GetWhereCondition();
        var groupBy = query?.GetGroupByStatements() ?? new List<Statement>();

        // Если ReplaceFrom вернул null (такое происходит в самом глубоком SELECT, т.е. у которого FROM (Table)),
        // необходимо начать собирать наш запрос
        query = query ?? baseQuery;

        baseQuery.ReplaceSelect(dict);
        baseQuery.ReplaceWhere(dict, whereCondition);
        baseQuery.ReplaceGroupBy(dict, groupBy);

        query.SetGroupByStatements(baseQuery.GetGroupByStatements());
        query.SetSelectStatements(baseQuery.GetSelectStatements());
        query.SetWhereCondition(baseQuery.GetWhereCondition());

        return query;
    }

    /// <summary>
    /// Создание соответствия [имя алиаса] -> Statement
    /// </summary>
    /// <param name="selectStatements"></param>
    /// <returns></returns>
    private static Dictionary<string, Statement> CalcColumnNameReplaceDict(List<Statement>? selectStatements)
    {
        Dictionary<string, Statement> selectAliases = new();

        if (selectStatements == null)
            return selectAliases;

        foreach (var selectItem in selectStatements)
        {
            var aliasItem = selectItem as Alias;
            if (aliasItem != null)
                selectAliases[aliasItem.GetAlias()] = aliasItem.GetDefinition();
        }

        return selectAliases;
    }

    /// <summary>
    /// Рекурсивная замена имен столбцов согласно соответствию [имя алиаса] -> Statement
    /// </summary>
    /// <param name="statements"></param>
    /// <param name="columnNameReplaceDict"></param>
    /// <returns></returns>
    private static Statement ReplaceAliases(List<Statement>? statements, Dictionary<string, Statement> columnNameReplaceDict)
    {
        for (int index = 0; index < statements.Count; index++)
        {
            var selectItem = statements[index];
            var columnItem = selectItem as Column;

            if (columnItem != null && columnNameReplaceDict.ContainsKey(columnItem.GetColumn()))
            {
                return columnNameReplaceDict[columnItem.GetColumn()];
            }

            var aliasItem = selectItem as Alias;
            if (aliasItem != null)
            {
                var result = ReplaceAliases(new List<Statement>() { aliasItem.GetDefinition() }, columnNameReplaceDict);
                if (result != null)
                    aliasItem.SetDefinition(result);
            }

            var functionItem = selectItem as FunctionCall;
            if (functionItem != null)
            {
                var arguments = functionItem.GetArguments().ToList();
                for (int i = 0; i < arguments.Count; i++)
                {
                    var result = ReplaceAliases(new List<Statement>() { arguments[i] }, columnNameReplaceDict);
                    if (result != null)
                        arguments[i] = result;
                }
                functionItem.SetArguments(arguments);
            }

            var binaryOperItem = selectItem as BinaryOperation;
            if (binaryOperItem != null)
            {
                var leftResult = ReplaceAliases(new List<Statement>() { binaryOperItem.GetLeftOperand() }, columnNameReplaceDict);
                if (leftResult != null)
                    binaryOperItem.SetLeftOperand(leftResult);

                var rightResult = ReplaceAliases(new List<Statement>() { binaryOperItem.GetRightOperand() }, columnNameReplaceDict);
                if (rightResult != null)
                    binaryOperItem.SetRightOperand(rightResult);
            }
        }
        return null;
    }

    /// <summary>
    /// Формирование блока SELECT
    /// </summary>
    /// <param name="query"></param>
    /// <param name="columnNameReplaceDict"></param>
    public static void ReplaceSelect(this SelectQuery query,
        Dictionary<string, Statement> columnNameReplaceDict)
    {
        var outerSelectStatements = query.GetSelectStatements();
        ReplaceAliases(outerSelectStatements, columnNameReplaceDict);
    }

    /// <summary>
    /// Формирование блока WHERE
    /// </summary>
    /// <param name="query"></param>
    /// <param name="columnNameReplaceDict"></param>
    /// <param name="oldWhereCondition"></param>
    public static void ReplaceWhere(this SelectQuery query,
        Dictionary<string, Statement> columnNameReplaceDict, Statement oldWhereCondition)
    {
        var outerWhereCondition = query.GetWhereCondition();
        ReplaceAliases(new List<Statement>() { outerWhereCondition }, columnNameReplaceDict);

        // Добавление WHERE из аргумента к имеющимся 
        if (oldWhereCondition == null)
            query.SetWhereCondition(outerWhereCondition);
        else if (outerWhereCondition == null)
            query.SetWhereCondition(oldWhereCondition);
        else
            query.SetWhereCondition(new BinaryOperation(outerWhereCondition, oldWhereCondition, "AND"));
    }

    /// <summary>
    /// Формирование блока GROUP BY. Предполагается, что внутри GROUP BY могут быть только Column
    /// </summary>
    /// <param name="query"></param>
    /// <param name="columnNameReplaceDict"></param>
    /// <param name="oldGroupByStatements"></param>
    public static void ReplaceGroupBy(this SelectQuery query,
        Dictionary<string, Statement> columnNameReplaceDict, List<Statement> oldGroupByStatements)
    {
        var outerGroupByStatements = query.GetGroupByStatements();

        var outerColumns = outerGroupByStatements?.Select(g => (g as Column)?.GetColumn())?.ToList()
            ?? new List<string?>();

        // Замена имен столбцов
        for (int index = 0; index < outerColumns.Count(); index++)
            if (columnNameReplaceDict.ContainsKey(outerColumns[index]))
                outerColumns[index] = (columnNameReplaceDict[outerColumns[index]] as Column)?.GetColumn();

        // Добавление GROUP BY из агрумента функции к имеющимся
        var oldColumns = oldGroupByStatements?.Select(g => (g as Column)?.GetColumn());
        if (oldColumns != null)
            outerColumns = outerColumns.Union(oldColumns).ToList();

        // Рекурсивное получение списка столбцов, которые используются в функциях
        // (столбцы под агрегатными функциями потом уберем из GROUP BY)
        var select = query.GetSelectStatements();
        var usedColumns = GetUsedColumnsFromStatementTree(select).Distinct();

        // Убираем из итогового списка столбцы, которые используются в функциях
        outerColumns = outerColumns.Except(usedColumns).ToList();

        var results = new List<Statement>();
        foreach (var column in outerColumns)
            results.Add(new Column(column));

        query.SetGroupByStatements(results.Any() ? results : []);
    }

    /// <summary>
    /// Рекурсивное получение списка столбцов, которые используются в функциях
    /// </summary>
    /// <param name="select"></param>
    /// <param name="inFunction">Флажок для учета только тех имен столбцов, которые внутри функций</param>
    /// <returns></returns>
    private static IEnumerable<string?> GetUsedColumnsFromStatementTree(List<Statement>? select, bool inFunction = false)
    {
        List<string?> result = [];

        foreach (var item in select)
        {
            var columnItem = item as Column;
            if (columnItem != null && inFunction)
                result.Add(columnItem.GetColumn());

            var aliasItem = item as Alias;
            if (aliasItem != null)
                result.AddRange(GetUsedColumnsFromStatementTree(new List<Statement>() { aliasItem.GetDefinition() }));

            var functionItem = item as FunctionCall;
            if (functionItem != null)
            {
                var arguments = functionItem.GetArguments().ToList();
                for (int i = 0; i < arguments.Count; i++)
                    result.AddRange(GetUsedColumnsFromStatementTree(new List<Statement>() { arguments[i] }, true));
            }

            var binaryOperItem = item as BinaryOperation;
            if (binaryOperItem != null)
            {
                result.AddRange(GetUsedColumnsFromStatementTree(new List<Statement>() { binaryOperItem.GetLeftOperand() }));
                result.AddRange(GetUsedColumnsFromStatementTree(new List<Statement>() { binaryOperItem.GetRightOperand() }));
            }
        }

        return result;
    }

    /// <summary>
    /// Формирование блока FROM (рекурсивно)
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public static SelectQuery ReplaceFrom(this SelectQuery query)
    {
        var fromStatements = query.GetFromStatements();
        var innerSelect = fromStatements.FirstOrDefault() as SelectQuery;

        if (innerSelect != null)
            return Optimize(innerSelect);
        
        return null;
    }
}