namespace PRN232.Domain.Entities;

public abstract class BaseEntity
{
    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
