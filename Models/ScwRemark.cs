namespace OeeSystem.Models;

public class ScwRemark
{
    public int Id { get; set; }
    public int Scw4MTypeId { get; set; }
    public Scw4MType? Scw4MType { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    
    public ICollection<ScwEvent> ScwEvents { get; set; } = new List<ScwEvent>();
}

