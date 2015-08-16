namespace Perst
{
    using System;
    using System.Diagnostics;
    
    /// <summary>
    /// Floating point operations with error
    /// </summary>
    internal class FP 
    {
        const double EPSILON = 1.0E-06;
        
        internal static bool zero(double x) { 
            return Math.Abs(x) <= EPSILON;
        }
        
        internal static bool eq(double x, double y) { 
            return zero(x - y);
        }
        
        internal static bool ne(double x, double y) { 
            return !eq(x, y);
        }
        
        internal static bool lt(double x, double y) { 
            return y - x > EPSILON;
        }
        
        internal static bool le(double x, double y) { 
            return x - y <= EPSILON;
        }
        
        internal static bool gt(double x, double y) { 
            return x - y > EPSILON;
        }
        
        internal static bool ge(double x, double y) { 
            return y - x <= EPSILON;
        }
    };
    
    
    /// <summary>
    /// Point in 3D
    /// </summary>
    internal class Point3D 
    {
        internal double x;
        internal double y;
        internal double z;
    
        internal Point3D cross(Point3D p) { 
            return new Point3D(y * p.z - z * p.y,
                               z * p.x - x * p.z,
                               x * p.y - y * p.x);
        }
         
        internal double distance() {
            return Math.Sqrt(x*x + y*y + z*z);
        }
        
        public override bool Equals(Object o) { 
            if (o is Point3D)  { 
                Point3D p = (Point3D)o;
                return FP.eq(x, p.x) && FP.eq(y, p.y) && FP.eq(z, p.z);
            }
            return false;
        } 
    
        internal RectangleRn toRectangle() { 
            return new RectangleRn(new double[]{x, y, z, x, y, z});
        }
        
        internal Sphere.Point toSpherePoint() { 
            double rho = Math.Sqrt(x*x + y*y);
            double lat, lng;
            if (0.0 == rho) {
                if (FP.zero(z)) {
                    lat = 0.0;
                } else if (z > 0) {
                    lat = Math.PI/2;
                } else {
                    lat = -Math.PI/2;
                } 
            } else {
                lat = Math.Atan(z / rho);
            }
    
            lng = Math.Atan2(y, x);
            if (FP.zero(lng)){
                lng = 0.0;
            } else { 
                if (lng < 0.0) {
                    lng += Math.PI*2 ;
                }
            }
            return new Sphere.Point(lng, lat);
        }       
    
    
        internal void addToRectangle(RectangleRn r) { 
            addToRectangle(r, x, y, z);
        }
    
        internal static void addToRectangle(RectangleRn r, double ra, double dec) { 
            double x = Math.Cos(ra)*Math.Cos(dec);
            double y = Math.Sin(ra)*Math.Cos(dec);
            double z = Math.Sin(dec);
            addToRectangle(r, x, y, z);
        }
    
        internal static void addToRectangle(RectangleRn r, double x, double y, double z) { 
            if (x < r.coords[0]) { 
                r.coords[0] = x;
            }
            if (y < r.coords[1]) { 
                r.coords[1] = y;
            }
            if (z < r.coords[2]) { 
                r.coords[2] = z;
            }
            if (x > r.coords[3]) { 
                r.coords[3] = x;
            }
            if (y > r.coords[4]) { 
                r.coords[4] = y;
            }
            if (z > r.coords[5]) { 
                r.coords[5] = z;
            }
        }       
         
        internal Point3D(double ra, double dec) {
            x = Math.Cos(ra)*Math.Cos(dec);
            y = Math.Sin(ra)*Math.Cos(dec);
            z = Math.Sin(dec);
        }
    
        internal Point3D() {}
    
        internal Point3D(Sphere.Point p) : this(p.ra, p.dec)
        {
        }
    
        internal Point3D(double x, double y, double z) { 
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
    
    /// <summary>
    ////Euler transformation
    /// </summary>
    internal class Euler  
    { 
        internal int phi_a;    // first axis
        internal int theta_a;  // second axis
        internal int psi_a;    // third axis
        internal double phi;   // first rotation angle
        internal double theta; // second rotation angle
        internal double psi;   // third rotation angle
    
        internal const int AXIS_X = 1;
        internal const int AXIS_Y = 2;
        internal const int AXIS_Z = 3;
    
        internal void transform(Point3D result, Point3D input)
        {
            int t = 0;
            double a, sa, ca;
            double x1, y1, z1;
            double x2, y2, z2;
            
            x1 = input.x;
            y1 = input.y;
            z1 = input.z;
    
            for (int i=0; i<3; i++) {
                switch (i) {
                case 0: 
                    a = phi; 
                    t = phi_a; 
                    break;
                case 1: 
                    a = theta; 
                    t = theta_a; 
                    break;
                default: 
                    a = psi; 
                    t = psi_a; 
                    break;
                }
                
                if (FP.zero(a)) {
                    continue;
                }
                
                sa = Math.Sin(a);
                ca = Math.Cos(a);
    
                switch (t) {                    
                case AXIS_X :
                    x2 = x1;
                    y2 = ca*y1 - sa*z1;
                    z2 = sa*y1 + ca*z1;
                    break;
                case AXIS_Y :
                    x2 = ca*x1 + sa*z1;
                    y2 = y1;
                    z2 = ca*z1 - sa*x1;
                    break;
                default:
                    x2 = ca*x1 - sa*y1;
                    y2 = sa*x1 + ca*y1;
                    z2 = z1;
                    break;               
                }
                x1 = x2;
                y1 = y2;
                z1 = z2;
            }
            result.x = x1;
            result.y = y1;
            result.z = z1;
        }
    }
    
    
    /// <summary>
    /// Class for conversion equatorial coordinates to Cartesian R3 coordinates
    /// </summary>
    public class Sphere 
    {
        /// <summary>
        /// Common interface for all objects on sphere
        /// </summary>
        public interface SphereObject 
        { 
            /// <summary>
            /// Get wrapping 3D rectangle for this object
            /// </summary>
            RectangleRn WrappingRectangle();
            
            /// <summary>
            /// Check if object contains specified point
            // <param name="p">sphere point</param>
            /// </summary>
            bool Contains(Point p);
        };
                
        /// <summary> 
        /// Class representing point of equatorial coordinate system (astronomic or terrestrial)
        /// </summary>
        public struct Point : SphereObject
        { 
            /// <summary> 
            /// Right ascension or longitude
            /// </summary>
            public double ra;  
            /// <summary>
            /// Declination or latitude
            /// </summary>
            public double dec; 
    
    
            public double Latitude
            {
               get
               { 
                   return dec;
               }
            }
    
            public double Longitude
            {
               get 
               { 
                   return ra;
               }
            }
    
            // Fast implementation from Q3C
            public double Distance(Point p) 
            { 
                double x = Math.Sin((ra - p.ra) / 2);
                x *= x;
                double y = Math.Sin((dec - p.dec) / 2);
                y *= y;
                double z = Math.Cos((dec + p.dec) / 2);
                z *= z;
                return 2 * Math.Asin(Math.Sqrt(x * (z - y) + y));
            }
    
            public override bool Equals(Object o) { 
                if (o is Point)  { 
                    Point p = (Point)o;
                    return FP.eq(ra, p.ra) && FP.eq(dec, p.dec);
                }
                return false;
            } 
    
            public RectangleRn WrappingRectangle() { 
                double x = Math.Cos(ra)*Math.Cos(dec);
                double y = Math.Sin(ra)*Math.Cos(dec);
                double z = Math.Sin(dec);
                return new RectangleRn(new double[]{x, y, z, x, y, z});
            }
    
            public PointRn ToPointRn() { 
                double x = Math.Cos(ra)*Math.Cos(dec);
                double y = Math.Sin(ra)*Math.Cos(dec);
                double z = Math.Sin(dec);
                return new PointRn(new double[]{x, y, z});
            }
    
            public bool Contains(Point p) { 
                return Equals(p);
            }
    
            public override String ToString() { 
                return "(" + ra + "," + dec + ")";
            }
    
            public Point(double ra, double dec)  {
                this.ra = ra;
                this.dec = dec;
            }
        }
    
        public struct Box : SphereObject
        { 
            public Point sw; // source-west
            public Point ne; // nord-east
    
            public override bool Equals(Object o) { 
                if (o is Box)  { 
                    Box b = (Box)o;
                    return sw.Equals(b.sw) && ne.Equals(b.ne);
                }
                return false;
            }
    
            public bool Contains(Point p) { 
                return Contains(p.ra, p.dec);
            }
    
            public bool Contains(double ra, double dec) { 
                if ((FP.eq(dec,ne.dec) && FP.eq(dec, Math.PI/2)) || (FP.eq(dec,sw.dec) && FP.eq(dec, -Math.PI/2)))  {
                    return true;
                }
    
                if (FP.lt(dec, sw.dec) || FP.gt(dec, ne.dec)) {
                    return false;
                }
                if (FP.gt(sw.ra, ne.ra)) {
                    if (FP.gt(ra, sw.ra) || FP.lt(ra, ne.ra)) { 
                        return false;
                    } 
                } else {
                    if (FP.lt(ra, sw.ra) || FP.gt(ra, ne.ra)) { 
                        return false;
                    }
                }
                return true;
            }
               
            public RectangleRn WrappingRectangle() { 
                RectangleRn r = sw.WrappingRectangle();
                double ra, dec;
                Point3D.addToRectangle(r, ne.ra, ne.dec);
                Point3D.addToRectangle(r, sw.ra, ne.dec);
                Point3D.addToRectangle(r, ne.ra, sw.dec);
                
                 // latitude closest to equator
                if (FP.ge(ne.dec, 0.0) && FP.le(sw.dec, 0.0)) {
                    dec = 0.0;
                } else if (Math.Abs(ne.dec) > Math.Abs(sw.dec)) {
                    dec = sw.dec;
                } else {
                    dec = ne.dec;
                }
    
                for (ra = 0.0; ra < Math.PI*2-0.1; ra += Math.PI/2) {
                    if (Contains(ra, dec)) { 
                        Point3D.addToRectangle(r, ra, dec);
                    }
                }
                return r;
            }
    
            public Box(Point sw, Point ne) { 
                this.sw = sw;
                this.ne = ne;
            }
        };
    
        public struct Circle : SphereObject
        {
            public Point center;
            public double radius;
            
            public Circle(Point center, double radius) { 
                this.center = center;
                this.radius = radius;
            }
            
            public override String ToString() { 
                return "<" + center + "," + radius + ">";
            }
    
            public override bool Equals(Object o) { 
                if (o is Circle)  { 
                    Circle c = (Circle)o;
                    return center.Equals(c.center) && FP.eq(radius, c.radius);
                }
                return false;
            }
    
            public bool Contains(Point p) { 
                double distance = center.Distance(p);
                return FP.le(distance, radius);
            }
    
            public RectangleRn WrappingRectangle() { 
                Point3D[] v = new Point3D[8];
                Point3D tv = new Point3D();
                Euler euler = new Euler();
    
                double r = Math.Sin(radius);
                double d = Math.Cos(radius);
    
                v[0] = new Point3D(-r, -r, d);
                v[1] = new Point3D(-r, +r, d);
                v[2] = new Point3D(+r, -r, d);
                v[3] = new Point3D(+r, +r, d);
                v[4] = new Point3D(-r, -r, 1.0);
                v[5] = new Point3D(-r, +r, 1.0);
                v[6] = new Point3D(+r, -r, 1.0);
                v[7] = new Point3D(+r, +r, 1.0);
                
                euler.psi_a    = Euler.AXIS_X;
                euler.theta_a  = Euler.AXIS_Z;
                euler.phi_a    = Euler.AXIS_X;
                euler.phi      = Math.PI/2 - center.dec;
                euler.theta    = Math.PI/2 + center.ra;
                euler.psi      = 0.0;
    
                Point3D min = new Point3D(1.0, 1.0, 1.0);
                Point3D max = new Point3D(-1.0, -1.0, -1.0);
    
                for (int i=0; i<8; i++) {
                    euler.transform(tv, v[i]);
                    if (tv.x < -1.0) {
                        min.x = -1.0;
                    } else if (tv.x > 1.0) {
                        max.x = 1.0;
                    } else { 
                        if (tv.x < min.x) { 
                            min.x = tv.x;
                        }
                        if (tv.x > max.x) { 
                            max.x = tv.x;
                        }
                    }
          
                    if (tv.y < -1.0) {
                        min.y = -1.0;
                    } else if ( tv.y > 1.0 ) {
                        max.y = 1.0;
                    } else {
                        if (tv.y < min.y) { 
                            min.y = tv.y;
                        }
                        if (tv.y > max.y) { 
                            max.y = tv.y;
                        }
                    }
                    if (tv.z < -1.0) {
                        min.z = -1.0;
                    }  else if (tv.z > 1.0) {
                        max.z = 1.0;
                    } else { 
                        if (tv.z < min.z) { 
                            min.z = tv.z;
                        }
                        if (tv.z > max.z) { 
                            max.z = tv.z;
                        }
                    } 
                }
                return new RectangleRn(new double[]{min.x, min.y, min.z, max.x, max.y, max.z});
            }
        }
         
        /// <summary>
        /// A spherical ellipse is represented using two radii and
        /// a Euler transformation ( ZXZ-axis ). The "untransformed"
        /// ellipse is located on equator at position (0,0). The 
        /// large radius is along equator.    
        /// </summary>
        public struct Ellipse : SphereObject
        {
            /// <summary> 
            /// The large radius of an ellipse in radians
            /// </summary>
            public double rad0; 
            /// <summary>
            /// The small radius of an ellipse in radians
            /// </summary>
            public double rad1; 
            /// <summary>
            /// The first  rotation angle around z axis
            /// </summary>
            public double phi;  
            /// <summary>
            /// The second rotation angle around x axis
            /// </summary>
            public double theta;
            /// <summary>
            /// The last   rotation angle around z axis
            /// </summary>
            public double psi;  
            
            public Ellipse(double rad0, double rad1, double phi, double theta, double psi) { 
                this.rad0 = rad0;
                this.rad1 = rad1;
                this.phi = phi;
                this.theta = theta;
                this.psi = psi;
            }            
            
            public Point Center() { 
                return new Point(psi, -theta);
            }
    
            public bool Contains(Point p) { 
                // too complex implementation
                throw new InvalidOperationException("Ellipse.Contains(Point)");
            }            
    
            public override bool Equals(Object o) { 
                if (o is Ellipse)  { 
                    Ellipse e = (Ellipse)o;
                    return FP.eq(rad0, e.rad0) && FP.eq(rad1, e.rad1) && FP.eq(phi, e.phi) && FP.eq(theta, e.theta) && FP.eq(psi, e.psi);
                }
                return false;
            }
    
            public RectangleRn WrappingRectangle() { 
                Point3D[] v = new Point3D[8];
                Point3D tv = new Point3D();
                Euler euler = new Euler();
    
                double r0 = Math.Sin(rad0);
                double r1 = Math.Sin(rad1);
                double d = Math.Cos(rad1);
    
                v[0] = new Point3D(d, -r0, -r1);
                v[1] = new Point3D(d, +r0, -r1);
                v[2] = new Point3D(d, -r0, r1);
                v[3] = new Point3D(d, +r0, r1);
                v[4] = new Point3D(1.0, -r0, -r1);
                v[5] = new Point3D(1.0, +r0, -r1);
                v[6] = new Point3D(1.0, -r0, r1);
                v[7] = new Point3D(1.0, +r0, r1);
                
                euler.psi_a    = Euler.AXIS_Z ;
                euler.theta_a  = Euler.AXIS_Y;
                euler.phi_a    = Euler.AXIS_X;
                euler.phi      = phi;
                euler.theta    = theta;
                euler.psi      = psi;
    
                Point3D min = new Point3D(1.0, 1.0, 1.0);
                Point3D max = new Point3D(-1.0, -1.0, -1.0);
    
                for (int i=0; i<8; i++) {
                    euler.transform(tv, v[i]);
                    if (tv.x < -1.0) {
                        min.x = -1.0;
                    } else if (tv.x > 1.0) {
                        max.x = 1.0;
                    } else { 
                        if (tv.x < min.x) { 
                            min.x = tv.x;
                        }
                        if (tv.x > max.x) { 
                            max.x = tv.x;
                        }
                    }
          
                    if (tv.y < -1.0) {
                        min.y = -1.0;
                    } else if ( tv.y > 1.0 ) {
                        max.y = 1.0;
                    } else {
                        if (tv.y < min.y) { 
                            min.y = tv.y;
                        }
                        if (tv.y > max.y) { 
                            max.y = tv.y;
                        }
                    }
                    if (tv.z < -1.0) {
                        min.z = -1.0;
                    }  else if (tv.z > 1.0) {
                        max.z = 1.0;
                    } else { 
                        if (tv.z < min.z) { 
                            min.z = tv.z;
                        }
                        if (tv.z > max.z) { 
                            max.z = tv.z;
                        }
                    } 
                }
                return new RectangleRn(new double[]{min.x, min.y, min.z, max.x, max.y, max.z});
            }
        }
                
        public struct Line : SphereObject
        {
            /// <summary>
            /// The first  rotation angle around z axis
            /// </summary>
            public double phi;  
            /// <summary>
            /// The second rotation angle around x axis
            /// </summary>
            public double theta;
            /// <summary>
            /// The last   rotation angle around z axis
            /// </summary>
            public double psi;  
            /// <summary> 
            /// The length of the line in radians
            /// </summary>
            public double length; 
            
            public override bool Equals(Object o) { 
                if (o is Line) { 
                    Line l = (Line)o;
                    return FP.eq(phi, l.phi) && FP.eq(theta, l.theta) && FP.eq(psi, l.psi) && FP.eq(length, l.length);
                }
                return false;
            }
    
            public bool Contains(Point p) { 
                Euler euler = new Euler();
                Point3D spt = new Point3D();
                euler.phi     = -psi;
                euler.theta   = -theta;
                euler.psi     = -phi;
                euler.psi_a   = Euler.AXIS_Z;
                euler.theta_a = Euler.AXIS_X;
                euler.phi_a   = Euler.AXIS_Z;
                euler.transform(spt, new Point3D(p));
                Point sp = spt.toSpherePoint();
                return FP.zero(sp.dec) && FP.ge(sp.ra, 0.0) && FP.le(sp.ra, length);
            }                      
                
            public static Line Meridian(double ra) { 
                return new Line(-Math.PI/2, Math.PI/2, ra < 0.0 ? Math.PI*2 + ra : ra, Math.PI);
            }
    
            public Line(double phi, double theta, double psi, double length) {
                this.phi = phi;
                this.theta = theta;
                this.psi = psi;
                this.length = length;
            }            
            
            public Line(Point beg, Point end) { 
                double l = beg.Distance(end);
                if (FP.eq(l, Math.PI)) {
                    Debug.Assert(FP.eq(beg.ra, end.ra));
                    phi = -Math.PI/2;
                    theta = Math.PI/2;
                    psi = beg.ra < 0.0 ? Math.PI*2 + beg.ra : beg.ra;
                    length = Math.PI;
                    return;
                }
                if (beg.Equals(end)) { 
                    phi    = Math.PI/2;
                    theta  = beg.dec;
                    psi    = beg.ra - Math.PI/2;
                    length = 0.0;
                } else { 
                    Point3D beg3d = new Point3D(beg);
                    Point3D end3d = new Point3D(end);
                    Point3D tp = new Point3D();
                    Point spt = beg3d.cross(end3d).toSpherePoint();
                    Euler euler = new Euler();
                    euler.phi     = - spt.ra - Math.PI/2;
                    euler.theta   =   spt.dec - Math.PI/2;
                    euler.psi     =   0.0 ;
                    euler.psi_a   = Euler.AXIS_Z;
                    euler.theta_a = Euler.AXIS_X;
                    euler.phi_a   = Euler.AXIS_Z;
                    euler.transform(tp, beg3d);
                    spt = tp.toSpherePoint();
    
                    // invert
                    phi = spt.ra;
                    theta = -euler.theta;
                    psi = -euler.phi;
                    length = l;
                }
            }
    
            public RectangleRn WrappingRectangle() { 
               Euler euler = new Euler();
               euler.phi      = phi;
               euler.theta    = theta;
               euler.psi      = psi;
               euler.psi_a    = Euler.AXIS_Z ;
               euler.theta_a  = Euler.AXIS_X;
               euler.phi_a    = Euler.AXIS_Z;
               
               if (FP.zero(length)) {
                   Point3D beg3d = new Point3D();
                   Point3D end3d = new Point3D();
                   euler.transform(beg3d, new Point3D(0.0, 0.0));
                   euler.transform(end3d, new Point3D(length, 0.0));
                   RectangleRn r = beg3d.toRectangle();
                   end3d.addToRectangle(r);
                   return r;
               } else { 
                   double l, ls, lc;
                   Point3D[] v = new Point3D[4];
                   Point3D tv = new Point3D();
                   l  = length / 2.0; 
                   ls = Math.Sin(l);
                   lc = Math.Cos(l);
                   euler.phi += l;
                   
                   v[0] = new Point3D(lc,  lc<0 ? -1.0 : -ls, 0.0);
                   v[1] = new Point3D(1.0, lc<0 ? -1.0 : -ls, 0.0);
                   v[2] = new Point3D(lc,  lc<0 ? +1.0 : +ls, 0.0);
                   v[3] = new Point3D(1.0, lc<0 ? +1.0 : +ls, 0.0) ;
    
                   Point3D min = new Point3D(1.0, 1.0, 1.0);
                   Point3D max = new Point3D(-1.0, -1.0, -1.0);
    
                   for (int i=0; i<4; i++) {
                       euler.transform(tv, v[i]);
                       if (tv.x < -1.0) {
                           min.x = -1.0;
                       } else if (tv.x > 1.0) {
                           max.x = 1.0;
                       } else { 
                           if (tv.x < min.x) { 
                               min.x = tv.x;
                           }
                           if (tv.x > max.x) { 
                               max.x = tv.x;
                           }
                       }
                       
                       if (tv.y < -1.0) {
                           min.y = -1.0;
                       } else if ( tv.y > 1.0 ) {
                           max.y = 1.0;
                       } else {
                           if (tv.y < min.y) { 
                               min.y = tv.y;
                           }
                           if (tv.y > max.y) { 
                               max.y = tv.y;
                           }
                       }
                       if (tv.z < -1.0) {
                           min.z = -1.0;
                       } else if (tv.z > 1.0) {
                           max.z = 1.0;
                       } else { 
                           if (tv.z < min.z) { 
                               min.z = tv.z;
                           }
                           if (tv.z > max.z) { 
                               max.z = tv.z;
                           }
                       } 
                   }  
                   return new RectangleRn(new double[]{min.x, min.y, min.z, max.x, max.y, max.z});
               }
           }
        }
    
        public struct Polygon : SphereObject 
        { 
            public Point[] points;
            
            public Polygon(Point[] points) { 
                this.points = points;
            }
    
            public bool Contains(Point p) { 
                throw new InvalidOperationException("Polygon.Contains(Point)");
            }            
    
            public RectangleRn WrappingRectangle() { 
                Line line = new Line(points[0], points[1]);
                RectangleRn wr = line.WrappingRectangle();
                for (int i=1; i < points.Length; i++) {
                    line = new Line(points[i], points[(i+1) % points.Length]);
                    wr.Join(line.WrappingRectangle());
                }
                return wr;
            }
        }
    }
}
