namespace MovieActorManager.Models
{
    public class MovieDetailsVM
    {
        public Movie movie { get; set; }
        public List<Actor> actors { get; set; }

        // Sentiment analysis properties
        public List<Tuple<string, double>> SentimentResults { get; set; }  // Reddit post and sentiment score
        public double OverallSentiment { get; set; }  // Overall sentiment score
        public string OverallSentimentCategory { get; set; }

    }
}
