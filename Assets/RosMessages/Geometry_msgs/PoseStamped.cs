namespace RosMessageTypes.Geometry_msgs
{
    public class PoseStamped
    {
        public RosMessageTypes.StdMsgs.Header header;
        public Pose pose;
    }

    public class Pose
    {
        public Point position;
        public Quaternion orientation;
    }

    public class Point
    {
        public double x;
        public double y;
        public double z;
    }

    public class Quaternion
    {
        public double x;
        public double y;
        public double z;
        public double w;
    }
}