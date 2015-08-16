namespace Perst
{
    using System;
    using System.Diagnostics;
    
    /// <summary>
    /// R-n rectangle class representing n-dimensional wrapping rectangle. This class is used in spatial index.
    /// </summary>
    public struct RectangleRn
    {
        internal double[] coords;

        /// <summary>
        /// Get minimal value for i-th coordinate of rectangle 
        /// </summary>
        public double GetMinCoord(int i) 
        { 
            return coords[i];
        }

        /// <summary>
        /// Get maximal value for i-th coordinate of rectangle 
        /// </summary>
        public double GetMaxCoord(int i) 
        { 
            return coords[coords.Length/2 + i];
        }


        /// <summary>
        /// Rectangle area
        /// </summary>
        public double Area() 
        { 
            double area = 1.0;
            for (int i = 0, n = coords.Length/2; i < n; i++) { 
                area *= coords[n+i] - coords[i];
            }
            return area;
        }

        /// <summary>
        /// Area of covered rectangle for two sepcified rectangles
        /// </summary>
        public static double JoinArea(RectangleRn a, RectangleRn b) 
        {
            double area = 1.0;
            for (int i = 0, n = a.coords.Length/2; i < n; i++) { 
                double min = Math.Min(a.coords[i], b.coords[i]);
                double max = Math.Max(a.coords[n + i], b.coords[n + i]);
                area *= max - min;
            }
            return area;
        }

        /// <summary>
        /// Calculate dostance from the specified poin to the rectange
        /// </summary>
        public double Distance(PointRn point) 
        { 
            double d = 0;
            for (int i = 0, n = point.coords.Length; i < n; i++) {             
                if (point.coords[i] < coords[i]) { 
                    d += (coords[i] - point.coords[i]) * (coords[i] - point.coords[i]);
                } else if (point.coords[i] > coords[n + i]) { 
                    d += (coords[n + i] - point.coords[i]) * (coords[n + i] - point.coords[i]);
                }
            }
            return Math.Sqrt(d);
        }    

        /// <summary>
        /// Create copy of the rectangle
        /// </summary>
        public RectangleRn(RectangleRn r) 
        {
            coords = new double[r.coords.Length];
            Array.Copy(r.coords, 0, coords, 0, coords.Length);
        }

        /// <summary>
        /// Construct n-dimensional rectangle using coordinates of two vertexes
        /// </summary>
        public RectangleRn(double[] coords) 
        { 
            this.coords = new double[coords.Length];
            Array.Copy(coords, 0,  this.coords, 0, coords.Length);
        }

        /// <summary>
        /// Construct rectangle with specified coordinates
        /// <param name="min">rectangle vertex with minimal coordinates</param>
        /// <param name="max">rectangle vertex with maximal coordinates</param>
        /// </summary>
        public RectangleRn(PointRn min, PointRn max) 
        { 
            int n = min.coords.Length;
            Debug.Assert(min.coords.Length == max.coords.Length);
            coords = new double[n*2];
            for (int i = 0; i < n; i++) {             
                Debug.Assert(min.coords[i] <= max.coords[i]);
                coords[i] = min.coords[i];
                coords[n+i] = max.coords[i];
            }
        }
        /// <summary>
        /// Join two rectangles. This rectangle is updates to contain cover of this and specified rectangle.
        /// </summary>
        /// <param name="r">rectangle to be joined with this rectangle
        /// </param>
        public void Join(RectangleRn r) 
        { 
            for (int i = 0, n = coords.Length/2; i < n; i++) 
            {             
                coords[i] = Math.Min(coords[i], r.coords[i]);
                coords[i+n] = Math.Max(coords[i+n], r.coords[i+n]);
            }
        }
    

        /// <summary>
        ///  Non destructive join of two rectangles. 
        /// </summary>
        /// <param name="a">first joined rectangle
        /// </param>
        /// <param name="b">second joined rectangle
        /// </param>
        /// <returns>rectangle containing cover of these two rectangles
        /// </returns>
        public static RectangleRn Join(RectangleRn a, RectangleRn b) 
        {
            RectangleRn r = new RectangleRn(a);
            r.Join(b);
            return r;
        }

        /// <summary>
        /// Checks if this rectangle intersects with specified rectangle
        /// </summary>
        public bool Intersects(RectangleRn r) 
        { 
            for (int i = 0, n = coords.Length/2; i < n; i++) {             
                if (coords[i+n] < r.coords[i] || coords[i] > r.coords[i+n]) { 
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if this rectangle contains the specified rectangle
        /// </summary>
        public bool Contains(RectangleRn r) 
        { 
            for (int i = 0, n = coords.Length/2; i < n; i++) {             
                if (coords[i] > r.coords[i] || coords[i+n] < r.coords[i+n]) { 
                    return false;
                }
            }
            return true;
        }
    }
}
