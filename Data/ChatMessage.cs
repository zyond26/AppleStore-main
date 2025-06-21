using System;
using System.Collections.Generic;

namespace WebsiteTMDT.Data;

public partial class ChatMessage
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Message { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Reply { get; set; }
}
