namespace ODataToSqlKata
{
    public class QueryParameters
    {
        public string Query { get; set; }
        public string Expand { get; set; }
        public int Limit { get; set; } = 25;
        public int Start { get; set; } = 0;
    }
}
