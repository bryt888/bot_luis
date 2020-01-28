

namespace Microsoft.BotBuilderSamples
{
    public class ConversationData
    {
        // The time-stamp of the most recent incoming message.
        public string Timestamp { get; set; }

        // The ID of the user's channel.
        public string ChannelId { get; set; }

        public string userId { get; set; }
        public string templocation { get; set; }
        public string querytime { get; set; }
        public string querystring { get; set; } = "";

        // Track whether we have already asked the user's name
        public bool PromptedUserForLocation { get; set; } = false;
        public bool PromptedUserForTimePeriod { get; set; } = false;
    }
}
