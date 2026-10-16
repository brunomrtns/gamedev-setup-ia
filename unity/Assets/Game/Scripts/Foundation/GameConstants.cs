namespace Studio.Foundation
{
    public static class GameConstants
    {
        // 3-Lane Horizontal Coordinates
        public const float LANE_LEFT_X = -2.2f;
        public const float LANE_CENTER_X = 0.0f;
        public const float LANE_RIGHT_X = 2.2f;
        public const float LANE_WIDTH = 2.2f;

        // Player Physical Constraints
        public const float DEFAULT_FORWARD_SPEED = 12.0f;
        public const float MAX_FORWARD_SPEED = 16.0f;
        public const float LANE_CHANGE_DURATION = 0.16f;
        
        public const float JUMP_HEIGHT = 2.2f;
        public const float JUMP_DURATION = 0.65f;
        public const float SLIDE_DURATION = 0.60f;

        // Track Dimensions
        public const float TRACK_TOTAL_LENGTH = 350.0f;
        public const float PORTAL_Z_POSITION = 350.0f;

        // Scoring
        public const int ENERGY_CELL_SCORE = 100;
        public const int POINTS_PER_METER = 10;

        // Tags & Names
        public const string TAG_PLAYER = "Player";
        public const string TAG_OBSTACLE = "Obstacle";
        public const string TAG_COLLECTIBLE = "Collectible";
        public const string TAG_PORTAL = "Finish";
    }
}
