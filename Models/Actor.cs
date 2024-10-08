using System.ComponentModel.DataAnnotations;

namespace MovieActorManager.Models
{
    public class Actor
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }
        public string? Gender { get; set; }
        public int? Age { get; set; }

        [Display(Name="IMDB Link")]
        public string? IMDBLink { get; set; }

        [Display(Name="Nationality")]
        public string? PhotoUrl { get; set; }

        // Image of Actor
        [DataType(DataType.Upload)]
        [Display(Name="Actor Image")]
        public byte[]? ActorImage { get; set; }

    }
}
