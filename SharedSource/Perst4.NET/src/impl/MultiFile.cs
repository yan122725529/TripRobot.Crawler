namespace Perst.Impl    
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Perst;
#if WINRT_NET_FRAMEWORK
    using Windows.Storage.Streams;
    using Windows.Storage;
    using System.Runtime.InteropServices.WindowsRuntime;
#endif

    public class MultiFile : IFile 
    { 
        public struct MultiFileSegment 
        { 
            public IFile f;
            public long  size;
        }

        long seek(long pos) 
        {
     	    currSeg = 0;
	    while (pos >= segment[currSeg].size) 
            { 
	        pos -= segment[currSeg].size;
	        currSeg += 1;
	    }
            return pos;
        }


        public void Write(long pos, byte[] b) 
        {
            pos = seek(pos);
            segment[currSeg].f.Write(pos, b);
        }

        public int Read(long pos, byte[] b) 
        { 
            pos = seek(pos);
            return segment[currSeg].f.Read(pos, b);
        }
        
        public void Sync()
        { 
            for (int i = segment.Length; --i >= 0;) 
            { 
                segment[i].f.Sync();
            }
        }
     
        public bool IsEncrypted
        {
            get 
            {
                return segment[0].f.IsEncrypted;
            }
        }

        public bool NoFlush
        {
            get 
            { 
                return parameters.noFlush; 
            }

            set 
            { 
                parameters.noFlush = value; 
                for (int i = segment.Length; --i >= 0;) 
                {
                     segment[i].f.NoFlush = value;
                }
            }
        }



        public void Lock(bool shared) 
        { 
            segment[0].f.Lock(shared);
        }

        public void Unlock() 
        { 
            segment[0].f.Unlock();
        }

        public void Close() 
        { 
            for (int i = segment.Length; --i >= 0;) 
            { 
                segment[i].f.Close();
            }
        }

        public MultiFile(MultiFileSegment[] segments)
        { 
            segment = segments;
            for (int i = 0; i < segments.Length; i++) 
            { 
                 fixedSize += segments[i].size;
            }
            fixedSize -= segment[segment.Length-1].size;
            segment[segment.Length-1].size = long.MaxValue;
        }

        public MultiFile(String[] segmentPath, long[] segmentSize, FileParameters parameters) 
        { 
            this.parameters = parameters;
            segment = new MultiFileSegment[segmentPath.Length];
            for (int i = 0; i < segment.Length; i++) 
            { 
                MultiFileSegment seg = new MultiFileSegment();
                seg.f = new OSFile(segmentPath[i], parameters);
                seg.size = segmentSize[i];
                fixedSize += seg.size;
                segment[i] = seg;
            }
            fixedSize -= segment[segment.Length-1].size;
            segment[segment.Length-1].size = long.MaxValue;
        }

        public MultiFile(String filePath, FileParameters parameters) 
        { 
#if WINRT_NET_FRAMEWORK
            System.Threading.Tasks.Task<Stream> t = ApplicationData.Current.LocalFolder.OpenStreamForReadAsync(filePath);
            t.Wait();
            StreamReader reader = new StreamReader(t.Result);
#else
            StreamReader reader = new StreamReader(filePath);
#endif
            this.parameters = parameters;
            segment = new MultiFileSegment[0];
            string line;
            while ((line = reader.ReadLine()) != null) 
            {
                int sepPos;
                MultiFileSegment seg = new MultiFileSegment();
                string path;
                if (line.StartsWith("\"")) 
                {
                    sepPos = line.IndexOf('"', 1);
                    path = line.Substring(1, sepPos-1);
                } 
                else 
                {
                    sepPos = line.IndexOf(' ');
                    path = sepPos < 0 ? line : line.Substring(0, sepPos);
                }
                if (sepPos >= 0 && sepPos+1 < line.Length) {
                    String fileLength = line.Substring(sepPos+1).Trim();
                    if (fileLength.Length > 0) 
                    { 
                        seg.size = long.Parse(fileLength)*1024; // kilobytes
                    }
                }
                fixedSize += seg.size;
                seg.f = new OSFile(path, parameters);
                MultiFileSegment[] newSegment = new MultiFileSegment[segment.Length+1];
                Array.Copy(segment, 0, newSegment, 0, segment.Length);
                newSegment[segment.Length] = seg;
                segment = newSegment;
            } 

            fixedSize -= segment[segment.Length-1].size;
            segment[segment.Length-1].size = long.MaxValue;
        }

        public long Length 
        {
            get 
            {
                return fixedSize +  segment[segment.Length-1].f.Length;
            }
        }

        MultiFileSegment[] segment;
        long               fixedSize;
        int                currSeg;
        FileParameters     parameters;
    }
}
