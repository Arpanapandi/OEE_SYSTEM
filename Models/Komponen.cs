namespace OeeSystem.Models;

public class Komponen
{
    public int Id { get; set; }
    public string PartNumber { get; set; } = string.Empty; // Maps to Part_Number
    public int? JmlKomponen { get; set; } // Maps to jml_komponen
    // Tambahkan field lain sesuai struktur tabel di DB_HOSS.dbo_komponen jika diperlukan
}

