using RussianBISqlOptimizer.Statements;
using RussianBISqlOptimizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleApp3.Helpers;

namespace ConsoleApp3.ReflectionStatement
{
    public static class StatementReflection
    {
        public static List<Statement>? GetSelectStatements(this SelectQuery baseQuery)
            => baseQuery.GetFieldValue("_selectStatements") as List<Statement>;
        public static void SetSelectStatements(this SelectQuery baseQuery, List<Statement> value)
            => baseQuery.SetFieldValue("_selectStatements", value);

        public static List<Statement>? GetFromStatements(this SelectQuery baseQuery)
            => baseQuery.GetFieldValue("_fromStatements") as List<Statement>;
        public static void SetFromStatements(this SelectQuery baseQuery, List<Statement> value)
            => baseQuery.SetFieldValue("_fromStatements", value);

        public static List<Statement>? GetGroupByStatements(this SelectQuery baseQuery)
            => baseQuery.GetFieldValue("_groupByStatements") as List<Statement>;
        public static void SetGroupByStatements(this SelectQuery baseQuery, List<Statement> value)
            => baseQuery.SetFieldValue("_groupByStatements", value);

        public static Statement? GetWhereCondition(this SelectQuery baseQuery)
            => baseQuery.GetFieldValue("_whereCondition") as Statement;
        public static void SetWhereCondition(this SelectQuery baseQuery, Statement value)
            => baseQuery.SetFieldValue("_whereCondition", value);


        public static string? GetColumn(this Column column)
            => column.GetFieldValue("_column") as string;
        public static void SetColumn(this Column column, string value)
            => column.SetFieldValue("_column", value);


        public static string? GetAlias(this Alias alias)
            => alias.GetFieldValue("_alias") as string;
        public static Statement? GetDefinition(this Alias alias)
            => alias.GetFieldValue("_definition") as Statement;
        public static void SetDefinition(this Alias alias, Statement value)
            => alias.SetFieldValue("_definition", value);


        public static Statement? GetLeftOperand(this BinaryOperation binaryOper)
            => binaryOper.GetFieldValue("_leftOperand") as Statement;
        public static void SetLeftOperand(this BinaryOperation binaryOper, Statement value)
            => binaryOper.SetFieldValue("_leftOperand", value);
        public static Statement? GetRightOperand(this BinaryOperation binaryOper)
            => binaryOper.GetFieldValue("_rightOperand") as Statement;
        public static void SetRightOperand(this BinaryOperation binaryOper, Statement value)
            => binaryOper.SetFieldValue("_rightOperand", value);


        public static IReadOnlyList<Statement>? GetArguments(this FunctionCall function)
            => function.GetFieldValue("_arguments") as IReadOnlyList<Statement>;
        public static void SetArguments(this FunctionCall function, IReadOnlyList<Statement> value)
            => function.SetFieldValue("_arguments", value);

    }
}
