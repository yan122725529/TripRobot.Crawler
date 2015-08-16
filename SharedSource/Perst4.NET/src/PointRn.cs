namespace Perst
{
    using System;

    /// <summary>
    /// R-n point class. This class is used in spatial index.
    /// </summary>
    public struct PointRn
    {
        internal double[] coords;
    
        /// <summary>
        /// Get value for i-th coordinate
        /// </summary>
        public double GetCoord(int i) 
        { 
            return coords[i];
        }
    
        /// <summary>
        /// Constructor
        /// </summary>
        public PointRn(double[] coords) 
        { 
            this.coords = new double[coords.Length];
            Array.Copy(coords, 0, this.coords, 0, coords.Length);
        }
    }
}