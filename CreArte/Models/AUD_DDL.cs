using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CreArte.Models;

public partial class AUD_DDL
{
    [Key]
    public int AUD_ID { get; set; }

    [Precision(3)]
    public DateTime FECHA { get; set; }

    [StringLength(128)]
    public string LOGIN_NAME { get; set; } = null!;

    [StringLength(128)]
    public string? HOST_NAME { get; set; }

    [StringLength(100)]
    public string? EVENTO { get; set; }

    [StringLength(256)]
    public string? OBJETO { get; set; }

    [StringLength(128)]
    public string? ESQUEMA { get; set; }

    public string? COMANDO { get; set; }
}
