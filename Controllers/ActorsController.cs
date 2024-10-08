using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
    public class ActorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Actors
        public async Task<IActionResult> Index()
        {
            return View(await _context.Actor.ToListAsync());
        }

        // GET: Actors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var actor = await _context.Actor.FirstOrDefaultAsync(m => m.Id == id);
            if (actor == null)
            {
                return NotFound();
            }

            // Initialize the ActorDetailsVM model
            ActorDetailsVM actorDetailsVM = new ActorDetailsVM
            {
                actor = actor
            };

            // Get the movies for this actor
            var movies = await (from mv in _context.Movie
                                join ma in _context.MovieActor on mv.Id equals ma.MovieId
                                where ma.ActorId == id
                                select mv)
                               .ToListAsync();

            actorDetailsVM.movies = movies;

            // Fetch Reddit posts
            List<string> redditPosts = await SearchRedditAsync(actor.Name);
            Console.WriteLine($"Reddit Posts Count: {redditPosts.Count}"); // Log count here

            // Check if there was an error fetching Reddit posts
            if (redditPosts.Any(post => post.StartsWith("Error")))
            {
                // Display the error message and skip sentiment analysis
                actorDetailsVM.SentimentResults = new List<Tuple<string, double>> { new Tuple<string, double>(redditPosts.First(), 0) };
                actorDetailsVM.OverallSentiment = 0;
                actorDetailsVM.OverallSentimentCategory = "Not Available";
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
                actorDetailsVM.SentimentResults = sentimentResults;
                actorDetailsVM.OverallSentiment = overallSentiment;

                // Categorize overall sentiment
                actorDetailsVM.OverallSentimentCategory = CategorizeSentiment(actorDetailsVM.OverallSentiment);
            }

            Console.WriteLine($"SentimentResults Count: {actorDetailsVM.SentimentResults.Count}");
            return View(actorDetailsVM);
        }


        //IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII
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


        // IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII





        // GET: Actors/Create
        public IActionResult Create()
        {
            return View();
        }

        public async Task<IActionResult> GetActorPhoto(int id)
        {
            var actor = await _context.Actor.FirstOrDefaultAsync(m => m.Id == id);
            if (actor == null)
            {
                return NotFound();
            }

            var imageData = actor.ActorImage;
            if (imageData == null || imageData.Length == 0)
            {
                return Content("No image available"); // Temporary debug message
            }

            return File(imageData, "image/jpeg"); // Use "image/jpeg" for broader support
        }


        // POST: Actors/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Gender,Age,IMDBLink,PhotoUrl")] Actor actor, IFormFile ActorImage)
        {
            ModelState.Remove(nameof(actor.ActorImage));  // Ensure model state is valid without ActorImage

            if (ModelState.IsValid)
            {
                if (ActorImage != null && ActorImage.Length > 0)
                {
                    var memoryStream = new MemoryStream();
                    await ActorImage.CopyToAsync(memoryStream);
                    actor.ActorImage = memoryStream.ToArray();  // Store the image in byte array format
                }
                else
                {
                    actor.ActorImage = new byte[0];  // If no image was uploaded, store an empty byte array
                }
                _context.Add(actor);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(actor);
        }


        // GET: Actors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var actor = await _context.Actor.FindAsync(id);
            if (actor == null)
            {
                return NotFound();
            }
            return View(actor);
        }

        // POST: Actors/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Gender,Age,IMDBLink,PhotoUrl")] Actor actor, IFormFile ActorImage)
        {
            if (id != actor.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(actor.ActorImage));  // Ensure model state is valid without ActorImage

            Actor existingActor = _context.Actor.AsNoTracking().FirstOrDefault(m => m.Id == id);

            if (ModelState.IsValid)
            {
                if (ActorImage != null && ActorImage.Length > 0)
                {
                    var memoryStream = new MemoryStream();
                    await ActorImage.CopyToAsync(memoryStream);
                    actor.ActorImage = memoryStream.ToArray();  // Store the image in byte array format
                }
                else if (existingActor != null)
                {
                    actor.ActorImage = existingActor.ActorImage;
                }
                else
                {
                    actor.ActorImage = new byte[0];  // If no image was uploaded, store an empty byte array
                }
                //_context.Add(actor);
                //await _context.SaveChangesAsync();
                //return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(actor);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ActorExists(actor.Id))
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
            return View(actor);
        }

        // GET: Actors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var actor = await _context.Actor
                .FirstOrDefaultAsync(m => m.Id == id);
            if (actor == null)
            {
                return NotFound();
            }

            return View(actor);
        }

        // POST: Actors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var actor = await _context.Actor.FindAsync(id);
            if (actor != null)
            {
                _context.Actor.Remove(actor);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ActorExists(int id)
        {
            return _context.Actor.Any(e => e.Id == id);
        }
    }
}