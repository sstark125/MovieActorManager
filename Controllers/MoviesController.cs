using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MovieActorManager.Data;
using MovieActorManager.Models;
using VaderSharp2;

namespace MovieActorManager.Controllers
{
    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MoviesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Movies
        public async Task<IActionResult> Index()
        {
            return View(await _context.Movie.ToListAsync());
        }

        // GET: Movies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }

            // Initialize the MovieDetailsVM model
            MovieDetailsVM movieDetailsVM = new MovieDetailsVM
            {
                movie = movie
            };

            // Get the actors for this movie
            var actors = await (from ac in _context.Actor
                                join ma in _context.MovieActor on ac.Id equals ma.ActorId
                                where ma.MovieId == id
                                select ac)
                               .ToListAsync();

            movieDetailsVM.actors = actors;

            // Fetch Reddit posts
            List<string> redditPosts = await SearchRedditAsync(movie.Title);
            Console.WriteLine($"Reddit Posts Count: {redditPosts.Count}"); // Log count here

            // Check if there was an error fetching Reddit posts
            if (redditPosts.Any(post => post.StartsWith("Error")))
            {
                // Display the error message and skip sentiment analysis
                movieDetailsVM.SentimentResults = new List<Tuple<string, double>> { new Tuple<string, double>(redditPosts.First(), 0) };
                movieDetailsVM.OverallSentiment = 0;
                movieDetailsVM.OverallSentimentCategory = "Not Available";
            }
            else
            {
                // Sentiment analysis setup
                SentimentIntensityAnalyzer analyzer = new SentimentIntensityAnalyzer();
                List<Tuple<string, double>> sentimentResults = new List<Tuple<string, double>>();
                double totalSentiment = 0;
                int validEntries = 0;

                // Analyze sentiment for each Reddit post
                foreach (var post in redditPosts)
                {
                    var sentiment = analyzer.PolarityScores(post);
                    if (sentiment.Compound != 0)  // Only include non-zero sentiment scores
                    {
                        sentimentResults.Add(new Tuple<string, double>(post, sentiment.Compound));
                        totalSentiment += sentiment.Compound;
                        validEntries++;
                    }
                }

                // Calculate the overall sentiment
                double overallSentiment = validEntries > 0 ? totalSentiment / validEntries : 0;
                movieDetailsVM.SentimentResults = sentimentResults;
                movieDetailsVM.OverallSentiment = overallSentiment;

                // Categorize overall sentiment
                movieDetailsVM.OverallSentimentCategory = CategorizeSentiment(movieDetailsVM.OverallSentiment);
            }

            Console.WriteLine($"SentimentResults Count: {movieDetailsVM.SentimentResults.Count}");
            return View(movieDetailsVM);
        }



        // IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII

        public static readonly HttpClient client = new HttpClient();
        [HttpGet]
        [HttpPost]
        public static async Task<List<string>> SearchRedditAsync(string searchQuery)
        {
            var returnList = new List<string>();

            try
            {
                using (WebClient wc = new WebClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    var json = await client.GetStringAsync("https://www.reddit.com/search.json?limit=100&q=" + HttpUtility.UrlEncode(searchQuery));

                    JsonDocument doc = JsonDocument.Parse(json);
                    JsonElement dataElement = doc.RootElement.GetProperty("data");
                    JsonElement childrenElement = dataElement.GetProperty("children");

                    foreach (JsonElement child in childrenElement.EnumerateArray())
                    {
                        if (child.TryGetProperty("data", out JsonElement data))
                        {
                            if (data.TryGetProperty("selftext", out JsonElement selftext))
                            {
                                string selftextValue = selftext.GetString();
                                if (!string.IsNullOrEmpty(selftextValue))
                                {
                                    returnList.Add(selftextValue);
                                }
                                else if (data.TryGetProperty("title", out JsonElement title)) // Use title if selftext is empty
                                {
                                    string titleValue = title.GetString();
                                    if (!string.IsNullOrEmpty(titleValue))
                                    {
                                        returnList.Add(titleValue);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                // Indicate that Reddit API is not accessible from Azure
                Console.WriteLine($"HTTP Request failed: {httpRequestException.Message}");
                returnList.Add("Error: Unable to access Reddit API from this server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                returnList.Add("Error: An unexpected issue occurred while trying to fetch Reddit data.");
            }

            return returnList;
        }



        private string CategorizeSentiment(double score)
        {
            if (score <= -0.75)
                return "Extremely Negative";
            else if (score > -0.75 && score <= -0.5)
                return "Very Negative";
            else if (score > -0.5 && score <= -0.1)
                return "Slightly Negative";
            else if (score >= 0.75)
                return "Extremely Positive";
            else if (score > 0.5 && score < 0.75)
                return "Very Positive";
            else if (score > 0.1 && score <= 0.5)
                return "Slightly Positive";
            else if (score > -0.1 && score < 0.1)
                return "Neutral";
            else
                return "Invalid Sentiment";
        }

        // GET: Movies/Create
        public IActionResult Create()
        {
            return View();
        }

        public async Task<IActionResult> GetMoviePhoto(int id)
        {
            var movie = await _context.Movie.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }

            var imageData = movie.MovieImage;
            if (imageData == null || imageData.Length == 0)
            {
                return Content("No image available"); // Temporary debug message
            }

            return File(imageData, "image/jpeg"); // Use "image/jpeg" for broader support
        }

        // POST: Movies/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,IMDBLink,Genre,YearOfRelease,PosterUrl")] Movie movie, IFormFile MovieImage)
        {
            ModelState.Remove(nameof(movie.MovieImage));

            if (ModelState.IsValid)
            {
                if (MovieImage != null && MovieImage.Length > 0)
                {
                    var memoryStream = new MemoryStream();
                    await MovieImage.CopyToAsync(memoryStream);
                    movie.MovieImage = memoryStream.ToArray();
                }
                else
                {
                    movie.MovieImage = new byte[0];
                }
                _context.Add(movie);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }

        // GET: Movies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie.FindAsync(id);
            if (movie == null)
            {
                return NotFound();
            }
            return View(movie);
        }

        // POST: Movies/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,IMDBLink,Genre,YearOfRelease,PosterUrl")] Movie movie, IFormFile MovieImage)
        {
            if (id != movie.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(movie.MovieImage));  // Ensure model state is valid without MovieImage

            Movie existingMovie = _context.Movie.AsNoTracking().FirstOrDefault(m => m.Id == id);

            if (ModelState.IsValid)
            {
                if (MovieImage != null && MovieImage.Length > 0)
                {
                    var memoryStream = new MemoryStream();
                    await MovieImage.CopyToAsync(memoryStream);
                    movie.MovieImage = memoryStream.ToArray();  // Store the image in byte array format
                }
                else if (existingMovie != null)
                {
                    movie.MovieImage = existingMovie.MovieImage;
                }
                else
                {
                    movie.MovieImage = new byte[0];  // If no image was uploaded, store an empty byte array
                }
                //_context.Add(movie);
                //await _context.SaveChangesAsync();
                //return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movie);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieExists(movie.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }

        // GET: Movies/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
            {
                return NotFound();
            }

            return View(movie);
        }

        // POST: Movies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var movie = await _context.Movie.FindAsync(id);
                if (movie != null)
                {
                    // Check for foreign key constraints
                    var hasActors = await _context.MovieActor.AnyAsync(ma => ma.MovieId == id);
                    if (hasActors)
                    {
                        // If the movie is associated with any actors, throw an exception
                        throw new Exception("This movie cannot be deleted because it is associated with one or more actors.");
                    }

                    _context.Movie.Remove(movie);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Log the exception (optional, depending on your logging setup)
                Console.WriteLine(ex.Message); // Replace with your logging mechanism

                // Return to the Delete view with an alert
                TempData["ErrorMessage"] = ex.Message; // Store the error message
                return RedirectToAction(nameof(Delete), new { id });
            }
        }


        private bool MovieExists(int id)
        {
            return _context.Movie.Any(e => e.Id == id);
        }
    }
}
