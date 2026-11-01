namespace Memoa.Functions.Data.Entities;

/// <summary>Assegnazione di un tag a un record, oppure la sua esclusione da parte dell'utente.</summary>
public sealed class RecordTag
{
    public int RecordId { get; set; }

    public Record Record { get; set; } = null!;

    public int TagId { get; set; }

    public Tag Tag { get; set; } = null!;

    public RecordTagSource Source { get; set; }

    /// <summary>Similarità che ha fatto assegnare il tag; solo per <see cref="RecordTagSource.Auto"/>.</summary>
    public double? Similarity { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum RecordTagSource
{
    /// <summary>Assegnato per similarità: si ricalcola a ogni rielaborazione del record o modifica del tag.</summary>
    Auto,

    /// <summary>Scelto dall'utente: l'assegnazione automatica non lo tocca.</summary>
    Manual,

    /// <summary>Tolto dall'utente: non è visibile e l'assegnazione automatica non lo riassegna.</summary>
    Excluded,
}
