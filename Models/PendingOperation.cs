namespace Models
{
    public enum OperationType
    {
        make,
        updateStock,
        delete
    }

    public class PendingOperation
    {
        public int PendingOperationId { get; set; }
        public OperationType Type { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int DeltaStock { get; set; }
        public string State { get; set; } = string.Empty;
        public int Tries { get; set; }

    }
}
