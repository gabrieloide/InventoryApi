namespace Models
{
    public class Product
    {
        public int ProductId {get; set;}
        public string Name {get; set;}
        public string Sku {get; set;}
        public int Stock {get; set;}
    }
    public class StockChanges
    {
        public string Date {get; set;}
        public int Delta {get; set;}
        public string Source {get; set;}

    }
}

