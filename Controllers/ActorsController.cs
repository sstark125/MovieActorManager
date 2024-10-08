using System;
using System.Collections.Generic;
using System.Linq;
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

            var actor = await _context.Actor
                .FirstOrDefaultAsync(m => m.Id == id);
            if (actor == null)
            {
                return NotFound();
            }

            ActorDetailsVM actorDetailsVM = new ActorDetailsVM();
            actorDetailsVM.actor = actor;

            //go get the movies from the db
            var movies = new List<Movie>();
            movies = await (from mv in _context.Movie
                            join ma in _context.MovieActor on mv.Id equals ma.MovieId
                            where ma.ActorId == id
                            select mv)
                           .ToListAsync();

            actorDetailsVM.movies = movies;

            List<string> redditPosts = await SearchRedditAsync(actor.Name);

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

            actorDetailsVM.OverallSentimentCategory = CategorizeSentiment(actorDetailsVM.OverallSentiment);

            return View(actorDetailsVM);
        }

        //IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII
        public static readonly HttpClient client = new HttpClient();
        [HttpGet]
        [HttpPost]
        
        public static async Task<List<string>> SearchRedditAsync(string searchQuery)
        {
            var returnList = new List<string>();
            var json = "";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                json = await client.GetStringAsync("https://www.reddit.com/search.json?limit=100&q=" + HttpUtility.UrlEncode(searchQuery));
            }

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
