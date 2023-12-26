namespace LearnCosmosDb;

public enum PhoneStatus
{
    Clear = 0,
    Grey = 1,
    Black = 2,
}

public class PhoneStatusInfo
{
    public string Id { get; set; }

    public string Phone { get; set; }

    public string LineOfBiz { get; set; }

    public string Comment { get; set; }

    public PhoneStatus Status { get; set; } = PhoneStatus.Clear;

    public DateTime CreateDate { get; set; } = DateTime.Now;

    public required List<PhoneStatusRow> History { get; set; } = new List<PhoneStatusRow>();
}

public class PhoneStatusRow
{
    public PhoneStatus Status { get; set; }

    public DateTime CreateDate { get; set; }
}