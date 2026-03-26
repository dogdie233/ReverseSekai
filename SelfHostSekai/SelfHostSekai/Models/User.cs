using System.ComponentModel.DataAnnotations;

namespace SelfHostSekai.Models;

public class User
{
    [Key]
    [MaxLength(36)]
    public required string Id { get; set; }
}