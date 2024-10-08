namespace MovieActorManager.Models
{
    public class ActorDetailsVM
    {
        public Actor actor { get; set; }
        public List<Movie> movies { get; set; }

        //------
        public List<Tuple<string, double>> SentimentResults { get; set; }  // Reddit post and sentiment score
        public double OverallSentiment { get; set; }  // Overall sentiment score
        public string OverallSentimentCategory { get; set; }
    }
}
