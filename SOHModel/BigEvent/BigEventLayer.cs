using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Model;
using SOHModel.Multimodal.Multimodal;

namespace SOHModel.BigEvent

{
    /// <summary>
    ///     This layer implements the <see cref="AbstractMultimodalLayer" /> to provide multi-modal routing capabilities.
    /// </summary>
    public class BigEventLayer : HumanTravelerLayer
    {
        // List of possible exit positions (exits) for the Barclays Arena
        private List<Position> _exitPositions;
        // List of possible bus stops for the Barclays Arena
        private List<Position> _busStops;

        public BigEventLayer()
        {
            // Initialize exit positions, e.g., predefined coordinates for the arena's exits
            _exitPositions = new List<Position>
            {
                new Position(9.900066, 53.589625), // VIP entry (less frequented)
                new Position(9.900056, 53.589583), // VIP entry (less frequented)
                new Position(9.900239, 53.589170), // entry for standing places (more frequented)
                new Position(9.900167, 53.589083), // main entry 1 (more frequented)
                new Position(9.898043, 53.589267), // main entry 2 (more frequented)
                new Position(9.900239, 53.589170), // entry for standing places (additional)
                new Position(9.900167, 53.589083), // main entry 1 (additional)
                new Position(9.898043, 53.589267), // main entry 2 (additional)
                new Position(9.900239, 53.589170), // entry for standing places (additional)
                new Position(9.900167, 53.589083), // main entry 1 (additional)
                new Position(9.898043, 53.589267) // main entry 2 (additional)
            };

            _busStops = new List<Position>
            {
                new Position(9.899348, 53.588574), // Arenen Bus Stop (Shuttle 380)
                new Position(9.899673, 53.592573), // Hellgrundweg (Arenen) Bus Stop (22)
                new Position(9.897838, 53.593081), // Hellgrundweg (Arenen) Bus Stop (22)
                new Position(9.908001, 53.585908), // Am Volkspark Bus Stop (180)
                new Position(9.908283, 53.585481) // Am Volkspark Bus Stop (180)
            };
        }

        /// <summary>
        /// Returns a random exit position for a visitor.
        /// </summary>
        public Position GetExitPosition()
        {
            var random = new Random();
            int index = random.Next(_exitPositions.Count);
            return _exitPositions[index];
        }
        
        public Position GetBusStopPosition()
        {
            var random = new Random();
            int index = random.Next(_busStops.Count);
            return _busStops[index];
        }
    }
}