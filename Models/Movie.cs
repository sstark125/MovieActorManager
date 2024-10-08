using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace MovieActorManager.Models
{
    public class Movie
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Display(Name = "IMDB Link")]
        public string? IMDBLink { get; set; }

        public string? Genre { get; set; }

        [Display(Name="Release Date")]
        public int? YearOfRelease { get; set; }

        [Display(Name ="Rating")]
        public string? PosterUrl { get; set; }

        //Image of Movie
        [DataType(DataType.Upload)]
        [Display(Name = "Movie Poster")]
        public byte[]? MovieImage { get; set; }



    }
}
