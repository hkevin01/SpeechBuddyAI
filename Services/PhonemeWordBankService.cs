using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

// ID: SVC-PHONEME-001
// Purpose: Returns position-aware practice word lists for common English phoneme targets.
// Supports initial, medial, and final positions for each target phoneme.
public sealed class PhonemeWordBankService
{
    // Key format: "phoneme:position" (e.g. "r:initial", "s:final", "sh:medial")
    private static readonly Dictionary<string, string[]> WordBank = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---- /r/ ----
        ["r:initial"]  = ["rabbit", "rain", "red", "rock", "river", "rope", "roof", "road"],
        ["r:medial"]   = ["arrow", "carrot", "barrel", "forest", "orange", "mirror", "hurry", "parent"],
        ["r:final"]    = ["car", "star", "door", "floor", "bear", "chair", "ear", "four"],

        // ---- /l/ ----
        ["l:initial"]  = ["leaf", "lamp", "light", "lemon", "lion", "lake", "log", "lip"],
        ["l:medial"]   = ["jello", "balloon", "elbow", "yellow", "silly", "color", "cellar", "pillow"],
        ["l:final"]    = ["ball", "bell", "bowl", "shell", "hill", "pool", "tail", "owl"],

        // ---- /s/ ----
        ["s:initial"]  = ["sun", "soup", "sock", "sand", "sail", "seal", "safe", "sink"],
        ["s:medial"]   = ["bison", "lesson", "muscle", "pencil", "listen", "basket", "castle", "vessel"],
        ["s:final"]    = ["bus", "grass", "house", "race", "mice", "goose", "juice", "dress"],

        // ---- /z/ ----
        ["z:initial"]  = ["zero", "zoo", "zipper", "zebra", "zap", "zone", "zoom", "zip"],
        ["z:medial"]   = ["fuzzy", "puzzle", "wizard", "blizzard", "buzzard", "lizard", "ooze", "razor"],
        ["z:final"]    = ["bees", "toes", "nose", "rose", "cheese", "breeze", "please", "freeze"],

        // ---- /sh/ ----
        ["sh:initial"] = ["ship", "shoe", "shop", "shell", "sheep", "shark", "share", "shine"],
        ["sh:medial"]  = ["ocean", "motion", "nation", "cushion", "fashion", "station", "fishing", "wishing"],
        ["sh:final"]   = ["fish", "wish", "dish", "brush", "push", "rush", "cash", "mesh"],

        // ---- /ch/ ----
        ["ch:initial"] = ["chair", "cheese", "chip", "chin", "chain", "chest", "cherry", "child"],
        ["ch:medial"]  = ["teacher", "kitchen", "peaches", "butcher", "catcher", "pitcher", "stitches", "matches"],
        ["ch:final"]   = ["beach", "teach", "reach", "speech", "couch", "touch", "bench", "lunch"],

        // ---- /th/ voiced ----
        ["th:initial"] = ["the", "this", "that", "them", "then", "there", "they", "though"],
        ["th:medial"]  = ["mother", "brother", "father", "weather", "feather", "whether", "other", "bathing"],
        ["th:final"]   = ["bathe", "teethe", "soothe", "clothe", "loathe", "breathe", "sheathe", "writhe"],

        // ---- /th/ voiceless ----
        ["thv:initial"] = ["think", "thin", "three", "throw", "thumb", "throne", "thread", "thirst"],
        ["thv:medial"]  = ["nothing", "bathtub", "birthday", "pathway", "toothbrush", "something", "athlete", "method"],
        ["thv:final"]   = ["bath", "path", "math", "mouth", "tooth", "cloth", "growth", "booth"],

        // ---- /f/ ----
        ["f:initial"]  = ["fish", "foot", "farm", "fan", "face", "fog", "fire", "fall"],
        ["f:medial"]   = ["coffee", "traffic", "trophy", "gopher", "loafer", "before", "affect", "office"],
        ["f:final"]    = ["leaf", "wolf", "chief", "reef", "loaf", "roof", "half", "elf"],

        // ---- /v/ ----
        ["v:initial"]  = ["van", "vine", "voice", "vest", "vase", "very", "vote", "view"],
        ["v:medial"]   = ["river", "shovel", "travel", "seven", "never", "even", "oven", "haven"],
        ["v:final"]    = ["cave", "dive", "live", "love", "give", "move", "wave", "brave"],

        // ---- /k/ ----
        ["k:initial"]  = ["cat", "kite", "coat", "cup", "key", "can", "call", "coin"],
        ["k:medial"]   = ["baking", "sticker", "chicken", "pocket", "rocket", "cookie", "pickle", "tackle"],
        ["k:final"]    = ["book", "lake", "bike", "sock", "back", "brick", "look", "neck"],

        // ---- /g/ ----
        ["g:initial"]  = ["goat", "girl", "game", "gate", "gift", "gold", "good", "green"],
        ["g:medial"]   = ["tiger", "sugar", "wagon", "figure", "dragon", "eagle", "igloo", "magnet"],
        ["g:final"]    = ["bag", "bug", "fog", "leg", "dog", "mug", "pig", "rug"],

        // ---- /t/ ----
        ["t:initial"]  = ["table", "top", "tall", "town", "toy", "take", "tap", "ten"],
        ["t:medial"]   = ["butter", "kitten", "button", "better", "bottle", "battle", "litter", "pretty"],
        ["t:final"]    = ["bat", "boat", "boot", "coat", "kite", "hat", "feet", "goat"],

        // ---- /d/ ----
        ["d:initial"]  = ["dog", "door", "down", "dark", "desk", "dip", "draw", "drop"],
        ["d:medial"]   = ["ladder", "muddy", "teddy", "noodle", "middle", "saddle", "puddle", "fiddle"],
        ["d:final"]    = ["bed", "bird", "cloud", "food", "hand", "head", "road", "seed"],

        // ---- /n/ ----
        ["n:initial"]  = ["nose", "name", "nail", "neck", "nest", "net", "night", "nine"],
        ["n:medial"]   = ["pony", "money", "honey", "funny", "sunny", "penny", "banana", "dinner"],
        ["n:final"]    = ["can", "fan", "moon", "rain", "run", "seen", "skin", "sun"],

        // ---- /m/ ----
        ["m:initial"]  = ["map", "milk", "moon", "mouse", "mop", "mud", "man", "more"],
        ["m:medial"]   = ["camel", "lemon", "hammer", "summer", "swimmer", "climbing", "stomach", "famous"],
        ["m:final"]    = ["arm", "dream", "drum", "farm", "foam", "form", "game", "home"],

        // ---- blends ----
        ["bl:initial"] = ["blue", "black", "blade", "blank", "blast", "blaze", "bless", "blow"],
        ["br:initial"] = ["broom", "bread", "brown", "brain", "bride", "brick", "bring", "branch"],
        ["sp:initial"] = ["spot", "spin", "spoon", "speed", "space", "spark", "spell", "sport"],
        ["st:initial"] = ["star", "stop", "step", "stem", "story", "stone", "store", "stack"],
        ["tr:initial"] = ["tree", "truck", "train", "track", "trip", "trust", "try", "trim"],
        ["fl:initial"] = ["flag", "flat", "flea", "flew", "flip", "float", "floor", "flow"],
        ["gr:initial"] = ["grape", "grass", "grab", "grill", "grin", "grow", "groom", "greet"],
        ["cl:initial"] = ["clock", "cloud", "claw", "clean", "clear", "click", "clip", "close"],
        ["pr:initial"] = ["prize", "print", "pray", "press", "proof", "proud", "price", "prop"],
        ["sn:initial"] = ["snake", "snap", "snail", "snow", "snug", "sniff", "snack", "sneeze"],
        ["sw:initial"] = ["swim", "swing", "swan", "sweet", "swift", "swam", "swipe", "sway"],
        ["pl:initial"] = ["play", "plan", "plus", "plate", "plant", "plane", "place", "plum"],
        ["sm:initial"] = ["smile", "smoke", "small", "smart", "smell", "smooth", "smack", "smash"],
        ["sl:initial"] = ["sled", "slip", "slow", "slam", "slate", "sleep", "slim", "slap"],
        ["fr:initial"] = ["frog", "from", "free", "fry", "fresh", "front", "freeze", "friend"],
        ["cr:initial"] = ["crab", "cry", "crow", "crust", "crate", "crack", "creek", "crown"],
    };

    private static readonly string[] FallbackWords = ["top", "cup", "bat", "net", "hop", "dig", "run", "wag"];

    // ID: SVC-PHONEME-002
    // Purpose: Returns words for a target sound, optionally scoped to a position.
    // Inputs: target (string, e.g. "r" or "sh"), position (string?, "initial"/"medial"/"final"/null)
    // Outputs: string[] with 4-8 unique words sorted alphabetically. Never null or empty.
    public string[] GetWords(string target, string? position = null)
    {
        var normalizedTarget = (target ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedPosition = (position ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return FallbackWords;
        }

        // Attempt exact key match first
        var key = string.IsNullOrWhiteSpace(normalizedPosition)
            ? null
            : $"{normalizedTarget}:{normalizedPosition}";

        if (key is not null && WordBank.TryGetValue(key, out var positioned))
        {
            return positioned;
        }

        // Collect all positions for the target
        var allForTarget = WordBank
            .Where(kv => kv.Key.StartsWith(normalizedTarget + ":", StringComparison.OrdinalIgnoreCase))
            .SelectMany(kv => kv.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(w => w)
            .ToArray();

        return allForTarget.Length > 0 ? allForTarget : FallbackWords;
    }

    // ID: SVC-PHONEME-003
    // Purpose: Returns all targets known to the word bank.
    public IReadOnlyList<string> KnownTargets()
    {
        return WordBank.Keys
            .Select(k => k.Split(':')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToArray();
    }
}
