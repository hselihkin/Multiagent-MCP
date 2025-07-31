using System.Text.Json;

namespace Agents
{
    /// <summary>
    /// Represents a memory management system for storing and retrieving contextual information.
    /// </summary>
    /// <remarks>The <see cref="PlannerMemory"/> class provides functionality to manage medium-term and
    /// long-term memory items. Medium-term memory is limited to a fixed number of items, and older items are
    /// automatically removed when the limit is exceeded. Long-term memory can be explicitly added and is also subject
    /// to a limit, with older items being removed when the limit is exceeded. This class supports adding new memory
    /// items, retrieving relevant memories based on a query context, and formatting memories for display.</remarks>
    public class PlannerMemory
    {
        private readonly List<MemoryItem> _mediumTermStore = new List<MemoryItem>();
        private readonly List<MemoryItem> _longTermStore = new List<MemoryItem>();
        private const int MaxMediumTermItems = 20; // Limit for medium-term memory
        private const int MaxLongTermItemsForContext = 5;

        public PlannerMemory()
        {
        }

        /// <summary>
        /// Adds a memory item to the medium-term memory store, with an option to also add it to the long-term memory
        /// store.
        /// </summary>
        /// <remarks>If the medium-term memory store exceeds its maximum capacity, the oldest memory item
        /// is automatically removed. Similarly, if the long-term memory store exceeds its maximum capacity, the oldest
        /// memory item is removed.</remarks>
        /// <param name="content">The content of the memory item. Cannot be null or empty.</param>
        /// <param name="type">The type or category of the memory item. Defaults to <see langword="Generic"/> if not specified.</param>
        /// <param name="source">The source or origin of the memory item. Defaults to <see langword="Unknown"/> if not specified.</param>
        /// <param name="tags">Optional metadata tags associated with the memory item. If <see langword="null"/>, an empty dictionary is
        /// used.</param>
        /// <param name="toLongTerm">A value indicating whether the memory item should also be added to the long-term memory store. <see
        /// langword="true"/> to add the item to long-term memory; otherwise, <see langword="false"/>.</param>
        public void AddMemory(string content, string type = "Generic", string source = "Unknown", Dictionary<string, string>? tags = null, bool toLongTerm = false)
        {
            var memoryItem = new MemoryItem
            {
                Content = content,
                Type = type,
                Source = source,
                Tags = tags ?? new Dictionary<string, string>()
            };
            // Add to medium-term memory
            _mediumTermStore.Add(memoryItem);
            Console.WriteLine($"[MEMORY ADDED - MEDIUM] {memoryItem}");

            if (_mediumTermStore.Count > MaxMediumTermItems)
            {
                _mediumTermStore.RemoveAt(0); // Remove oldest item if limit exceeded
                Console.WriteLine("[MEMORY TRIMMED - MEDIUM] Oldest item removed.");
            }

            if (toLongTerm)
            {
                // Add to long-term memory
                _longTermStore.Add(memoryItem);
                if (_longTermStore.Count > MaxLongTermItemsForContext)
                {
                    _longTermStore.RemoveAt(0); // Remove oldest item if limit exceeded
                    Console.WriteLine("[MEMORY TRIMMED - LONG] Oldest item removed.");
                }
                Console.WriteLine($"[MEMORY ADDED - LONG] {memoryItem}");
            }
        }

        /// <summary>
        /// Retrieves a list of relevant memory items based on the provided query context.
        /// </summary>
        /// <remarks>This method combines medium-term and long-term memories to produce a result set. If
        /// <paramref name="currentQueryContext"/> is provided, the method filters long-term memories based on matching
        /// tags or content with keywords extracted from the query context.</remarks>
        /// <param name="currentQueryContext">The context of the current query, used to filter and prioritize relevant memories. If null or whitespace,
        /// the method retrieves the most recent memories without filtering.</param>
        /// <param name="maxMedium">The maximum number of medium-term memories to retrieve. Defaults to 5.</param>
        /// <param name="maxLong">The maximum number of long-term memories to retrieve. Defaults to 3.</param>
        /// <returns>A list of <see cref="MemoryItem"/> objects that are relevant to the query context. The list is ordered by
        /// descending timestamp and contains distinct items.</returns>
        public List<MemoryItem> RetrieveRelevantMemories(string currentQueryContext, int maxMedium = 5, int maxLong = 3)
        {
            var relevantMemories = new List<MemoryItem>();
            relevantMemories.AddRange(_mediumTermStore.TakeLast(maxMedium));
            if (!string.IsNullOrWhiteSpace(currentQueryContext))
            {
                var queryKeywords = currentQueryContext.ToLower().Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
                relevantMemories.AddRange(_longTermStore
                    .Where(m => m.Tags.Any(tag => queryKeywords.Contains(tag.Value.ToLower()) || queryKeywords.Contains(tag.Key.ToLower())) ||
                                 queryKeywords.Any(qk => m.Content.ToLower().Contains(qk)))
                    .TakeLast(maxLong));
            }
            else
            {
                relevantMemories.AddRange(_longTermStore.TakeLast(maxLong));
            }
            Console.WriteLine($"[MEMORY RETRIEVED] Found {relevantMemories.Count} relevant memories for context: '{currentQueryContext.Substring(0, Math.Min(30, currentQueryContext.Length))}(...)'");
            return relevantMemories.DistinctBy(m => m.Id).OrderByDescending(m => m.Timestamp).ToList();
        }

        public string FormatMemoriesForPrompt(List<MemoryItem> memories)
        {
            if (!memories.Any()) return "No relevant memories found.";
            return string.Join("\n", memories.Select(m => $"- ({m.Type} from {m.Source} at {m.Timestamp:s}): {m.Content}"));
        }


    }

    /// <summary>
    /// Represents a unit of memory containing content, metadata, and associated tags.
    /// </summary>
    /// <remarks>A <see cref="MemoryItem"/> encapsulates information such as the content, type, source, and
    /// timestamp,  along with optional tags for categorization or relevance. This class is commonly used to store and 
    /// retrieve contextual data in applications that require memory-like structures, such as AI systems or  knowledge
    /// bases.</remarks>
    public class MemoryItem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Type { get; set; } = "Generic"; // e.g., "Fact", "AgentResult", "UserPreference"
        public string Source { get; set; } = "Unknown"; // e.g., "MetaDataAgent", "PlannerObservation"
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(); // For relevance, e.g., "assetId": "asset123"

        public override string ToString()
        {
            return $"[MemoryItem ({Type} from {Source} at {Timestamp:s})]: {Content} (Tags: {JsonSerializer.Serialize(Tags)})";
        }
    }

}
