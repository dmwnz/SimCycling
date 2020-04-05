using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

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
        public Vector3 XyzPoint { get; }
        public PointGeo WgsPoint { get; }
        public float Angle { get; }
        public float AltitudeFactor { get; }
        public float LongitudeFactor { get; }
        public float LatitudeFactor { get; }

        public ReferencePoint(Vector3 xyz, PointGeo wgs, float angle,
            float altitudeFactor = 1.0f, float longitudeFactor = 0.9875f, float latitudeFactor = 0.9875f)
        {
            this.XyzPoint = xyz;
            this.WgsPoint = wgs;
            this.Angle = angle;
            this.AltitudeFactor = altitudeFactor;
            this.LongitudeFactor = longitudeFactor;
            this.LatitudeFactor = latitudeFactor;
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
                new ReferencePoint(new Vector3(163.0779f, -406.1667f, -84.8346f), new PointGeo(44.34423f, 11.71605f, 40.0f), 1.1f)) },
            { "ks_nordschleife", new PointPair(
                new ReferencePoint(new Vector3(645.9265f, 1431.3629f, 88.9466f), new PointGeo(50.346426f, 6.966673f, 570.0f), -1.63f)) },
            { "trento-bondone", new PointPair(
                new ReferencePoint(new Vector3(2184.2517f, -2467.7197f, 73.1256f), new PointGeo(46.076604f, 11.098074f, 315.0f), -5.75f, 0.939351f)) },
            { "simtraxx_transfagarasan_v0.8", new PointPair(
                new ReferencePoint(new Vector3(-1461.8225f, -3900.5601f, 1.1499f), new PointGeo(45.675077f,24.57853f, 625.0f), -0.15f, longitudeFactor: 0.943f, latitudeFactor: 1.005f)) },
            { "saintroch", new PointPair(
                new ReferencePoint(new Vector3(1579.9989f, 3691.002f, -233.8722f), new PointGeo(43.883496f, 7.360775f, 655.0f), 0.0f)) },
            { "simtraxx_peyre_0.96", new PointPair(
                new ReferencePoint(new Vector3(-761.2817f, 761.537f, -39.8738f), new PointGeo(44.03069f, 3.67904f, 245.0f), 180.2f)) },
            { "simtraxx_pikes_peak_0.81", new PointPair(
                new ReferencePoint(new Vector3(3510.4993f, -3431.7925f, 139.0046f), new PointGeo(38.921214f, -105.037467f, 2866.0f), -27.81f)) }
        };

        public static float DegToRad(float angle)
        {
            return angle * (float) Math.PI / 180.0f;
        }

        public static Vector3 RotatePoint(Vector3 point, Vector3 reference, float angle)
        {
            var theta = DegToRad(angle);
            var ox = reference.X;
            var oy = reference.Y;

            var px = point.X;
            var py = point.Y;


            float x = (float) Math.Cos(theta) * (px - ox) - (float) Math.Sin(theta) * (py - oy) + ox;
            float y = (float) Math.Sin(theta) * (px - ox) + (float) Math.Cos(theta) * (py - oy) + oy;

            return new Vector3(x, y, point.Z);
        }

        public static float Norm(Vector3 point)
        {
            var dx = point.X;
            var dy = point.Y;
            var dz = point.Z;
            return (float) Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }


        private static PointGeo SXYZWGS(float x, float y, float z, Vector3 trackOriginXYZ, PointGeo trackOriginWGS, float altitudeFactor, float longitudeFactor, float latitudeFactor)
        {
            var x0 = trackOriginXYZ.X;
            var y0 = trackOriginXYZ.Y;
            var z0 = trackOriginXYZ.Z;

            var lon0 = trackOriginWGS.Longitude;
            var lat0 = trackOriginWGS.Latitude;
            var ele0 = trackOriginWGS.Elevation;


            var mPerDegree = 2 * Math.PI * R / 360.0f ;


            var latitude = lat0 - (y - y0) / (latitudeFactor * mPerDegree);
            var longitude = lon0 + (x - x0) / (longitudeFactor * mPerDegree * (float) Math.Cos(DegToRad((float) latitude)));

            var deltaElevation = (z - z0) * altitudeFactor;
            var elevation = ele0 + deltaElevation;

            return new PointGeo((float)latitude, (float) longitude, elevation);
        }

        public static PointGeo XYZToWGS(Vector3 xyzPoint, String track)
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

            var altitudeFactor = trackOrigins[track].Start.AltitudeFactor;
            var longitudeFactor = trackOrigins[track].Start.LongitudeFactor;
            var latitudeFactor = trackOrigins[track].Start.LatitudeFactor;

            var wgsPoint = SXYZWGS(x, y, z, startTrackOriginXYZ, startTrackOriginWGS, altitudeFactor, longitudeFactor, latitudeFactor);

            if (trackOrigins[track].End != null)
            {
                var xyzPointEnd = RotatePoint(xyzPoint, trackOrigins[track].End.XyzPoint, trackOrigins[track].End.Angle);
                var xe = xyzPointEnd.X;
                var ye = xyzPointEnd.Y;
                var ze = xyzPointEnd.Z;
                var endTrackOriginXYZ = trackOrigins[track].End.XyzPoint;
                var endTrackOriginWGS = trackOrigins[track].End.WgsPoint;
                var wgsPointEnd = SXYZWGS(xe, ye, ze, endTrackOriginXYZ, endTrackOriginWGS, altitudeFactor, longitudeFactor, latitudeFactor);
                var distanceFromStart = Norm(startTrackOriginXYZ - xyzPoint);
                var distanceToEnd = Norm(endTrackOriginXYZ - xyzPoint);

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
