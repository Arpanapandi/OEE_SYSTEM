namespace OeeSystem.Models;

public class Scw4MType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // "Material", "Method", "Machine", "Man", "No Problem"
    public string Code { get; set; } = string.Empty; // "MATERIAL", "METHOD", "MACHINE", "MAN", "NO_PROBLEM"
    public int DisplayOrder { get; set; } // Untuk urutan tampilan
    
    public ICollection<ScwRemark> Remarks { get; set; } = new List<ScwRemark>();
}

