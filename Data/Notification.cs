using System;
using System.Collections.Generic;

namespace WebsiteTMDT.Data;

public partial class Notification
{
    public int NotificationId { get; set; }

    public string? Content { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}
