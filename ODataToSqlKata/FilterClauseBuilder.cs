using Microsoft.OData.UriParser;
using SqlKata;
using System;
using System.Globalization;
using System.Linq;

namespace ODataToSqlKata
{
    public class FilterClauseBuilder : QueryNodeVisitor<Query>
    {
        private const DateTimeStyles ValidDateTimeSales = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces;
        private Query _query;

        public FilterClauseBuilder(Query query)
        {
            _query = query;
        }

        public override Query Visit(BinaryOperatorNode nodeIn)
        {
            var left = nodeIn.Left;
            if (left.Kind == QueryNodeKind.Convert)
            {
                left = (left as ConvertNode).Source;
            }

            var right = nodeIn.Right;
            if (right.Kind == QueryNodeKind.Convert)
            {
                right = (right as ConvertNode).Source;
            }

            switch (nodeIn.OperatorKind)
            {
                case BinaryOperatorKind.Or:
                case BinaryOperatorKind.And:
                    _query = _query.Where(where =>
                    {
                        var leftBuilder = new FilterClauseBuilder(where);
                        var leftQuery = left.Accept(leftBuilder);

                        if (nodeIn.OperatorKind == BinaryOperatorKind.Or)
                        {
                            leftQuery = leftQuery.Or();
                        }

                        var rightBuilder = new FilterClauseBuilder(leftQuery);
                        return right.Accept(rightBuilder);
                    });
                    break;

                case BinaryOperatorKind.Equal:
                case BinaryOperatorKind.NotEqual:
                case BinaryOperatorKind.GreaterThan:
                case BinaryOperatorKind.GreaterThanOrEqual:
                case BinaryOperatorKind.LessThan:
                case BinaryOperatorKind.LessThanOrEqual:
                    string op = GetOperatorString(nodeIn.OperatorKind);

                    if (left.Kind == QueryNodeKind.UnaryOperator)
                    {
                        _query = _query.Where(where =>
                        {
                            var leftBuilder = new FilterClauseBuilder(where);
                            return left.Accept(leftBuilder);
                        });

                        left = (left as UnaryOperatorNode).Operand;
                    }

                    if (right.Kind == QueryNodeKind.Constant)
                    {
                        var value = GetConstantValue(right);

                        if (left.Kind == QueryNodeKind.SingleValueFunctionCall)
                        {
                            var functionNode = left as SingleValueFunctionCallNode;
                            _query = ApplyFunction(_query, functionNode, op, value);
                        }
                        else
                        {
                            string column = GetColumnName(left);
                            _query = _query.Where(column, op, value);
                        }
                    }
                    break;

                default:
                    return _query;
            }

            return _query;
        }

        public override Query Visit(SingleValueFunctionCallNode nodeIn)
        {
            if (nodeIn is null)
            {
                throw new ArgumentNullException(nameof(nodeIn));
            }

            var nodes = nodeIn.Parameters.ToArray();

            return nodeIn.Name.ToLowerInvariant() switch
            {
                "contains" => _query.WhereContains(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true),
                "endswith" => _query.WhereEnds(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true),
                "startswith" => _query.WhereStarts(GetColumnName(nodes[0]), (string)GetConstantValue(nodes[1]), true),
                _ => _query,
            };
        }

        public override Query Visit(UnaryOperatorNode nodeIn)
        {
            switch (nodeIn.OperatorKind)
            {
                case UnaryOperatorKind.Not:
                    _query = _query.Not();

                    if (nodeIn.Operand.Kind == QueryNodeKind.SingleValueFunctionCall || nodeIn.Operand.Kind == QueryNodeKind.BinaryOperator)
                    {
                        return nodeIn.Operand.Accept(this);
                    }

                    return _query;

                default:
                    return _query;
            }
        }

        private static Query ApplyFunction(Query query, SingleValueFunctionCallNode leftNode, string operand, object rightValue)
        {
            var columnName = GetColumnName(leftNode.Parameters.FirstOrDefault());

            switch (leftNode.Name.ToUpperInvariant())
            {
                case "YEAR":
                case "MONTH":
                case "DAY":
                case "HOUR":
                case "MINUTE":
                    query = query.WhereDatePart(leftNode.Name, columnName, operand, rightValue);
                    break;
                case "DATE":
                    query = query.WhereDate(columnName, operand,
                        rightValue is DateTime date
                            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture.DateTimeFormat)
                            : rightValue);
                    break;
                case "TIME":
                    query = query.WhereTime(columnName, operand,
                        rightValue is DateTime time
                            ? time.ToString("HH:mm", CultureInfo.InvariantCulture.DateTimeFormat)
                            : rightValue);
                    break;
                default:
                    break;
            }

            return query;
        }

        private static bool ConvertToDateTimeUTC(string dateTimeString, out DateTime? dateTime)
        {
            if (DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture.DateTimeFormat, ValidDateTimeSales, out var dateTimeValue))
            {
                dateTime = dateTimeValue;
                return true;
            }
            else
            {
                dateTime = null;
                return false;
            }
        }

        private static string GetColumnName(QueryNode node)
        {
            var column = string.Empty;
            if (node.Kind == QueryNodeKind.Convert)
            {
                node = (node as ConvertNode).Source;
            }

            if (node.Kind == QueryNodeKind.SingleValuePropertyAccess)
            {
                column = (node as SingleValuePropertyAccessNode).Property.Name.Trim();
            }

            if (node.Kind == QueryNodeKind.SingleValueOpenPropertyAccess)
            {
                column = (node as SingleValueOpenPropertyAccessNode).Name.Trim();
            }

            return column;
        }

        private static object GetConstantValue(QueryNode node)
        {
            if (node.Kind == QueryNodeKind.Convert)
            {
                return GetConstantValue((node as ConvertNode).Source);
            }
            else if (node.Kind == QueryNodeKind.Constant)
            {
                var value = (node as ConstantNode).Value;
                if (value is string)
                {
                    var trimedValue = value.ToString().Trim();
                    if (ConvertToDateTimeUTC(trimedValue, out var dateTime))
                    {
                        return dateTime.Value;
                    }

                    return trimedValue;
                }

                return value;
            }
            else if (node.Kind == QueryNodeKind.CollectionConstant)
            {
                return (node as CollectionConstantNode).Collection.Select(cn => GetConstantValue(cn));
            }

            return null;
        }

        private static string GetOperatorString(BinaryOperatorKind operatorKind)
        {
            return operatorKind switch
            {
                BinaryOperatorKind.Equal => "=",
                BinaryOperatorKind.NotEqual => "<>",
                BinaryOperatorKind.GreaterThan => ">",
                BinaryOperatorKind.GreaterThanOrEqual => ">=",
                BinaryOperatorKind.LessThan => "<",
                BinaryOperatorKind.LessThanOrEqual => "<=",
                BinaryOperatorKind.Or => "or",
                BinaryOperatorKind.And => "and",
                BinaryOperatorKind.Add => "+",
                BinaryOperatorKind.Subtract => "-",
                BinaryOperatorKind.Multiply => "*",
                BinaryOperatorKind.Divide => "/",
                BinaryOperatorKind.Modulo => "%",
                _ => throw new NotImplementedException()
            };
        }
    }
}
