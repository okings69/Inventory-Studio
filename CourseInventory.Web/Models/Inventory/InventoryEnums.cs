namespace CourseInventory.Web.Models.Inventory;

public enum InventoryFieldType
{
    SingleLineText = 1,
    MultiLineText = 2,
    Number = 3,
    Link = 4,
    Boolean = 5
}

public enum CustomIdElementType
{
    FixedText = 1,
    Random20Bit = 2,
    Random32Bit = 3,
    Random6Digits = 4,
    Random9Digits = 5,
    Guid = 6,
    DateTime = 7,
    Sequence = 8
}
