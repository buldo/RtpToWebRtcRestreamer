namespace SIPSorcery.Media
{
    /// <summary>
    /// Band data for G722 Codec
    /// </summary>
    public class Band
    {
        /// <summary>s</summary>
        public int s;
        /// <summary>sp</summary>
        public int sp;
        /// <summary>sz</summary>
        public int sz;
        /// <summary>r</summary>
        public int[] r = new int[3];
        /// <summary>a</summary>
        public int[] a = new int[3];
        /// <summary>ap</summary>
        public int[] ap = new int[3];
        /// <summary>p</summary>
        public int[] p = new int[3];
        /// <summary>d</summary>
        public int[] d = new int[7];
        /// <summary>b</summary>
        public int[] b = new int[7];
        /// <summary>bp</summary>
        public int[] bp = new int[7];
        /// <summary>sg</summary>
        public int[] sg = new int[7];
        /// <summary>nb</summary>
        public int nb;
        /// <summary>det</summary>
        public int det;
    }
}