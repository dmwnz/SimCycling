using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimCycling.Utils
{
    public class PointPair
    {
        public ReferencePoint Start { get; }
        public ReferencePoint End { get; }
        public PointPair(ReferencePoint startPoint, ReferencePoint endPoint = null)
        {
            this.Start = startPoint;
            this.End = endPoint;
        }
    }

    public class ReferencePoint
    {
        public Point3D XyzPoint { get; }
        public PointGeo WgsPoint { get; }
        public float Angle { get; }

        public ReferencePoint(Point3D xyz, PointGeo wgs, float angle)
        {
            this.XyzPoint = xyz;
            this.WgsPoint = wgs;
            this.Angle = angle;
        }
    }

    public class Point3D
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public Point3D(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }


    public class PointGeo
    {
        public float Latitude { get; private set; }
        public float Longitude { get; private set; }
        public float Elevation { get; private set; }

        public PointGeo(float latitude, float longitude, float elevation)
        {
            this.Latitude = latitude;
            this.Longitude = longitude;
            this.Elevation = elevation;
        }
        public void AverageWith(PointGeo otherPoint, float coeffSelf, float coeffOther)
        {
            Latitude = (coeffSelf * Latitude + coeffOther * otherPoint.Latitude) / (coeffSelf + coeffOther);
            Longitude = (coeffSelf * Longitude + coeffOther * otherPoint.Longitude) / (coeffSelf + coeffOther);
            Elevation = (coeffSelf * Elevation + coeffOther * otherPoint.Elevation) / (coeffSelf + coeffOther);
        }
    }

    public static class Consts
    {
        public static readonly string BASE_OUT_PATH = 
            String.Format(@"{0}\SimCycling", 
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        static readonly float R = 6378137.0f;

        static readonly Dictionary<String, PointPair> trackOrigins = new Dictionary<string, PointPair> {
                { "imola", new PointPair(
                    new ReferencePoint(new Point3D(163.0779f, -406.1667f, -84.8346f), new PointGeo(44.34423f, 11.71605f, 40.0f), 1.1f)) },
                { "ks_nordschleife", new PointPair(
                    new ReferencePoint(new Point3D(645.9265f, 1431.3629f, 88.9466f), new PointGeo(50.346426f, 6.966673f, 570.0f), -1.63f)) },
                { "trento-bondone", new PointPair(
                    new ReferencePoint(new Point3D(2184.2517f, -2467.7197f, 73.1256f), new PointGeo(46.07667f, 11.09802f, 315.0f), -6.25f),
                    new ReferencePoint(new Point3D(-1108.7559f, 1298.8573f, 1457.8204f), new PointGeo(46.04035f, 11.06093f, 1620.0f), -5.65f)) },
                { "simtraxx_transfagarasan_v0.8", new PointPair(
                    new ReferencePoint(new Point3D(-1461.8225f, -3900.5601f, 1.1499f), new PointGeo(45.6752f, 24.57861f, 625.0f), 0.0f)) } };

        public static float DegToRad(float angle)
        {
            return angle * (float)Math.PI / 180.0f;
        }

        public static Point3D RotatePoint(Point3D point, Point3D reference, float angle)
        {
            var theta = DegToRad(angle);
            var ox = reference.X;
            var oy = reference.Y;

            var px = point.X;
            var py = point.Y;


            var x = (float)Math.Cos(theta) * (px - ox) - (float)Math.Sin(theta) * (py - oy) + ox;
            var y = (float)Math.Sin(theta) * (px - ox) + (float)Math.Cos(theta) * (py - oy) + oy;

            return new Point3D(x, y, point.Z);
        }

        public static float CalcDistance(Point3D pointA, Point3D pointB)
        {
            var dx = pointA.X - pointB.X;
            var dy = pointA.Y - pointB.Y;
            var dz = pointA.Z - pointB.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }


        private static PointGeo SXYZWGS(float x, float y, float z, Point3D trackOriginXYZ, PointGeo trackOriginWGS)
        {
            var x0 = trackOriginXYZ.X;
            var y0 = trackOriginXYZ.Y;
            var z0 = trackOriginXYZ.Z;

            var lon0 = trackOriginWGS.Longitude;
            var lat0 = trackOriginWGS.Latitude;
            var ele0 = trackOriginWGS.Elevation;


            var mPerDegree = 2 * (float)Math.PI * R / 360.0f;


            var latitude = lat0 - (y - y0) / mPerDegree;
            var longitude = lon0 + (x - x0) / (mPerDegree * (float)Math.Cos(DegToRad(latitude)));

            var deltaElevation = z - z0;
            var elevation = ele0 + deltaElevation;

            return new PointGeo(latitude, longitude, elevation);
        }

        public static PointGeo XYZToWGS(Point3D xyzPoint, String track)
        {

            if (!trackOrigins.ContainsKey(track))
            {
                return new PointGeo(0, 0, 0);
            }

            var xyzPointStart = RotatePoint(xyzPoint, trackOrigins[track].Start.XyzPoint, trackOrigins[track].Start.Angle);

            var x = xyzPointStart.X;
            var y = xyzPointStart.Y;
            var z = xyzPointStart.Z;

            var startTrackOriginXYZ = trackOrigins[track].Start.XyzPoint;
            var startTrackOriginWGS = trackOrigins[track].Start.WgsPoint;

            var wgsPoint = SXYZWGS(x, y, z, startTrackOriginXYZ, startTrackOriginWGS);

            if (trackOrigins[track].End != null)
            {
                var xyzPointEnd = RotatePoint(xyzPoint, trackOrigins[track].End.XyzPoint, trackOrigins[track].End.Angle);
                var xe = xyzPointEnd.X;
                var ye = xyzPointEnd.Y;
                var ze = xyzPointEnd.Z;
                var endTrackOriginXYZ = trackOrigins[track].End.XyzPoint;
                var endTrackOriginWGS = trackOrigins[track].End.WgsPoint;
                var wgsPointEnd = SXYZWGS(xe, ye, ze, endTrackOriginXYZ, endTrackOriginWGS);
                var distanceFromStart = CalcDistance(startTrackOriginXYZ, xyzPoint);
                var distanceToEnd = CalcDistance(endTrackOriginXYZ, xyzPoint);

                wgsPoint.AverageWith(wgsPointEnd, 1.0f / distanceFromStart, 1.0f / distanceToEnd);
            }

            return wgsPoint;
        }

        public static ushort ConvertGrade(float percent)
        {
            return (ushort)(((percent + 200.0f) / 400.0f) * 40000);
        }
    }
}
