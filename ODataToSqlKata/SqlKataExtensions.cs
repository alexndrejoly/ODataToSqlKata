using Microsoft.OData.UriParser;
using SqlKata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ODataToSqlKata
{
    public static class SqlKataExtensions
    {
        public static Query ApplyQueryParameters(this Query query,
          QueryParameters queryParams)
        {
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var fromClause = query.Clauses.OfType<FromClause>().FirstOrDefault();

            if (fromClause is null)
            {
                throw new ArgumentNullException(nameof(fromClause));
            }

            var result = EdmModelHelper.BuildTableModel(fromClause.Table);
            var model = result.Item1;
            var entityType = result.Item2;
            var entitySet = result.Item3;

            var queryOptions = new Dictionary<string, string>
            {
                { "filter", queryParams.Query }
            };

            var parser = new ODataQueryOptionParser(model, entityType, entitySet, queryOptions);
            parser.Resolver.EnableCaseInsensitive = true;
            parser.Resolver.EnableNoDollarQueryOptions = true;

            var filterClause = parser.ParseFilter();


            if (filterClause != null)
            {
                query = filterClause.Expression.Accept(new FilterClauseBuilder(query));
            }

            return query;
        }
    }
}
