using System;
using System.Collections.Generic;
using System.Windows.Media.Animation;
using System.IO;

namespace CY
{
    class GirlList
    {
        public GirlList()
        {
            ListOfGirls = new List<Girl>();
        }
        public List<Girl> ListOfGirls { get; set; }
    }
    public class GirlComp
    {
        // Compares by Height, Length, and Width.
        public static string Compare(Girl oldGirl, Girl newGirl)
        {
            string output = "";
            //if (oldGirl.Name.CompareTo(newGirl.Name) != 0)
            output += $"{oldGirl.LinkId}\n";
            output += $"{oldGirl.Name};{newGirl.Name}\n";

            if (oldGirl.Videos.Count.CompareTo(newGirl.Videos.Count) != 0)
                output += $"Videos:{oldGirl.Videos};{newGirl.Videos.Count}\n";

            if (oldGirl.Images.Count.CompareTo(newGirl.Images.Count) != 0)
                output += $"Images:{oldGirl.Images.Count};{newGirl.Images.Count}\n";
            if (oldGirl.Socials == null)
                oldGirl.Socials = "";
            if (newGirl.Socials == null)
                newGirl.Socials = "";
            if (oldGirl.Socials.CompareTo(newGirl.Socials) != 0)
                output += $"Socials:{oldGirl.Socials};{newGirl.Socials}\n";

            if (oldGirl.City.CompareTo(newGirl.City) != 0)
                output += $"City:{oldGirl.City};{newGirl.City}\n";

            if (oldGirl.BirthDateAsIs.CompareTo(newGirl.BirthDateAsIs) != 0)
                output += $"BirthDateAsIs:{oldGirl.BirthDateAsIs};{newGirl.BirthDateAsIs}\n";

            return output;
        }
    }
    public class Girl//IEnumeratble<Girl><--------------GirlList
    {
        public Girl()
        {
            Images = new List<string>();
            Videos = new List<string>();
        }
        public long Id { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string BirthDateAsIs { get; set; }
        public DateTime BirthDate { get; set; }
        public DateTime AddDate { get; set; }
        public DateTime DateOfState { get; set; }
        public string Link { get; set; }
        public int LinkId { get; set; }
        public string Socials { get; set; }
        public decimal AgeThen { get; set; }
        public int Rating { get; set; }
        public int Views { get; set; }
        public string Ava { get; set; }

        private string notes;
        public string Notes
        {
            get
            {
                if (notes == null)
                    return null;
                return notes;
            }
            set
            {
                if (notes != value)
                {
                    notes = value;
                    Notify?.Invoke(this);
                }
            }
        }
        public delegate void GirlHandler(Girl girl);
        public event GirlHandler Notify;
        public List<string> Images { get; set; }
        public List<string> Videos { get; set; }
        public string LastAccess { get; set; }
        public string LastUpdate { get; set; }
    }

    class VideoGirls
    {
        public List<VideoProperties> Properties { get; set; }
        public VideoGirls()
        {
            Properties = new List<VideoProperties>();
        }
    }
    class VideoProperties
    {
        public string GirlPage { get; set; }
        public List<Video> Videos { get; set; }
        public VideoProperties()
        {
            Videos = new List<Video>();
        }
    }
    class Video
    {
        public string Url { get; set; }
        public string Filename
        {
            get
            {
                if (Url == null) return null;
                return Path.GetFileName(Url);
            }

        }
        private string size;
        public string Size
        {
            get
            {
                return StringSizeFormat(size);
            }

            set
            {
                size = value;
            }
        }

        public string Quality { get; set; }
        public string Duration { get; set; }

        private string notes;
        public string Notes
        {
            get
            {
                if (notes == null)
                    return null;
                return notes;
            }
            set
            {
                if (notes != value)
                {
                    notes = value;
                    Notify?.Invoke(this);
                }
            }
        }
        public delegate void VideoHandler(Video video);
        public event VideoHandler Notify;
        public long girlId { get; set; }

        private string StringSizeFormat(string sizeInBytes)
        {
            //TODO something
            return sizeInBytes;
            if (string.IsNullOrEmpty(sizeInBytes))
                return "";
            decimal dec = Convert.ToDecimal(sizeInBytes);
            if (dec < 1024m * 1024m)
                return string.Format("{0:0.000}", Convert.ToDecimal(sizeInBytes) / 1024 / 1024);
            return string.Format("{0:#}", Convert.ToDecimal(sizeInBytes) / 1024 / 1024);
        }

    }
}
