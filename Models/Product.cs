namespace Models
{
    public class Product
    {
        public int ProductId {get; set;}
        public string Name {get; set;} = string.Empty;
        public string Sku {get; set;} = string.Empty;
        public int Stock {get; set;}
        public List<StockChanges> StockChanges {get; set;} = new List<StockChanges>();
    }
    public class StockChanges
    {
        public int StockChangesId {get; set;}
        public int ProductId {get; set;}
        public DateTimeOffset Date {get; set;}
        public int Delta {get; set;}
        public string Source {get; set;} = string.Empty;

    }
}

