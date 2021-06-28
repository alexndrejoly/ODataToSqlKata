using SqlKata;
using SqlKata.Compilers;
using System;

namespace ODataToSqlKata
{
    class Program
    {
        private static string CompileSqlKataQuery(Query query)
        {
            var sqlCompiler = new SqlServerCompiler();
            var sqlResult = sqlCompiler.Compile(query);
            return sqlResult.ToString();
        }


        static void Main(string[] args)
        {
            var query = new Query("Beneficiaire")
                .Select("Prenom")
                .Select("Nom")
                .Select("Age")
                .Select("DateNaissance");

            query = query.ApplyQueryParameters(new QueryParameters()
            {
                Query = "startswith(Prenom, 'Thib') or (Age ge 10 and Age le 90) or date(DateNaissance) gt '1993-01-01'"
            });

            var sql = CompileSqlKataQuery(query);

            Console.WriteLine(sql);

            Console.WriteLine("Hello World!");
        }
    }
}
