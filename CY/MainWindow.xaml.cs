using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HtmlAgilityPack;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Data.SQLite;
using YamlDotNet.Serialization;
using Xabe.FFmpeg;
using Cookie = System.Net.Cookie;
using System.Net.Http;
using Path = System.IO.Path;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Newtonsoft.Json;
using System.Configuration;
using System.Xml.Serialization;
using System.Windows.Shapes;
using Serilog;
using System.Security.Policy;

namespace CY
{
    public class MySettings
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string RootDatabaseFileFolder { get; set; }
        public string Host { get; set; }
    }
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Debug.WriteLine(DateTime.Now);
        }
        private void SettingsDeserializer()
        {
            var serializer = new XmlSerializer(typeof(MySettings));
            using (Stream stream = File.Open("settings.xml", FileMode.Open))
            {
                var settings = serializer.Deserialize(stream) as MySettings;
                UserLogin = settings.Username;
                UserPassword = settings.Password;
                RootDatabaseFileFolder = settings.RootDatabaseFileFolder;
                Host = "https://" + settings.Host;
                Dispatcher.Invoke(() => LoginTB.Text = UserLogin);
                Dispatcher.Invoke(() => PWTB.Password = UserPassword);
            }
        }
        string RootDatabaseFileFolder { get; set; }
        static HttpClientHandler handler { get; set; } = new HttpClientHandler();
        HttpClient httpClient;
        private CookieCollection _cc = new CookieCollection();
        private CookieContainer container = new CookieContainer();
        private string Host { get; set; }
        private SQLiteConnection DB;
        const string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:86.0) Gecko/20100101 Firefox/86.0";
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SettingsDeserializer();
            MainFolderPath = FolderPath();
            Uri uri = new Uri(Host);
            httpClient = new HttpClient(handler) { BaseAddress = uri };
            handler.CookieContainer = container;
            handler.UseCookies = true;
            await AddNotes();//TODO ШО за хрень
        }
        private List<string> GetDBLinks()
        {
            List<string> db_girls_links = new List<string>();

            if (!File.Exists(ManageDataGrids.DBPath))
                MessageBox.Show("Нужно создать базу данных");
            else
            {
                using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
                {
                    DB.Open();
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = "select link from Test";

                        using (SQLiteDataReader SQL = CMD.ExecuteReader())
                        {
                            if (SQL.HasRows)
                            {
                                while (SQL.Read())
                                {
                                    Debug.WriteLine(SQL.GetString(0));
                                    db_girls_links.Add(SQL.GetString(0).Trim());
                                }
                            }
                        }
                    }
                }
            }
            return db_girls_links;
        }

        private async void DownloadVideo_FromMenu(object sender, RoutedEventArgs e)
        {
            var items = Dispatcher.Invoke(() => DataGridVideosInfo.SelectedItems);
            if (items == null || items.Count == 0)
                return;
            var item = DataGridVideosInfo.SelectedItem as Video;
            string link = item.Url;
            await DownloadFile(link);

        }
        private async Task DownloadFile(string link)
        {
            string fileName = Path.GetFileName(link);
            await DownloadFile(link, fileName);
        }
        private async Task DownloadFile(string link, string fileName)
        {
            var stream = httpClient.GetStreamAsync(link).Result;
            byte[] chunk = new byte[8192 * 2];
            int length = 0;
            using (FileStream fs = new FileStream(fileName, FileMode.Append))
                while ((length = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    fs.Write(chunk, 0, length);
                }
        }

        private void DownloadFileSync(string link, string fileName)
        {
            int attempts = 0;
            string txt = "";
            while (true)
                try
                {
                    long fileSize = 0;
                    Stream stream;
                    HttpRequestMessage requestMessage;
                    long serverFileSize = 0;
                    long totalBytesReceived = 0;
                    using (var request = httpClient.GetAsync(link, HttpCompletionOption.ResponseHeadersRead).Result)
                    {
                        if (request.Content.Headers.ContentLength == null)
                        {
                            request.Dispose();
                            //throw new Exception("null Content-Length");
                            Debug.WriteLine("Нулевой ответ " + fileName);
                            return;
                        }
                        serverFileSize = Convert.ToInt64(request.Content.Headers.ContentLength);
                    }
                    if (File.Exists(fileName))
                    {
                        FileInfo fileInfo = new FileInfo(fileName);
                        fileSize = fileInfo.Length;
                        if (fileSize == serverFileSize)
                        {
                            return;
                        }
                        else if (fileSize > serverFileSize)
                        {
                            //throw new Exception("Файл на диске больше чем на сервере!");
                            Debug.WriteLine("Файл на диске больше чем на сервере! " + fileName);
                            return;
                        }
                        totalBytesReceived = fileSize;
                        //else
                        //    return;//Что-то сделатьBGetAllPages
                        requestMessage = new HttpRequestMessage(HttpMethod.Get, link);
                        requestMessage.Headers.Add("Range", $"bytes={fileSize}-{serverFileSize}");
                        var responseMessage = httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead).Result;
                        stream = responseMessage.Content.ReadAsStreamAsync().Result;
                    }
                    else
                    {
                        requestMessage = new HttpRequestMessage(HttpMethod.Get, link);
                        var responseMessage = httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead).Result;
                        stream = responseMessage.Content.ReadAsStreamAsync().Result;
                    }
                    byte[] chunk = new byte[65536];
                    using (FileStream fs = new FileStream(fileName, FileMode.Append))
                    {
                        while (totalBytesReceived < serverFileSize)
                        {
                            int length = stream.Read(chunk, 0, chunk.Length);
                            fs.Write(chunk, 0, length);
                            totalBytesReceived += length;
                        }
                        stream.Close();
                    }
                    if (totalBytesReceived != serverFileSize)
                        //MessageBox.Show($"Недокачался {fileName}");
                        _ = 0;
                    break;
                }
                catch (Exception e)
                {
                    attempts += 1;
                    if (attempts == 1)
                    {
                        txt = Dispatcher.Invoke(() => tBlockId.Text);
                        txt = $"{txt}, попьітка:{attempts}, {Path.GetFileName(link)}, {e.Message}";
                    }
                    else
                        txt = Regex.Replace(txt, @"попьітка:\d+", $"попьітка:{attempts}");
                    Dispatcher.Invoke(() => tBlockId.Text = txt);
                    if (attempts > 9)
                    {
                        MessageBox.Show("Слишком много попьіток^ " + link);
                        break;
                    }
                    //MessageBox.Show(e.Message);
                    _ = 0;
                }
        }
        private void Mirror_or_nor()
        {
            Host = "https://" + ConfigurationManager.AppSettings["WebHost"];
        }
        private int IsIntValid(string textInt)
        {
            if (!int.TryParse(textInt, out int max))
            {
                return -1;
            }
            return max;
        }
        string UserLogin, UserPassword;
        private void GetLoginPassword()
        {
            if (UserLogin == null || UserPassword == null)
            {
                UserLogin = Dispatcher.Invoke(() => LoginTB.Text);
                UserPassword = Dispatcher.Invoke(() => PWTB.Password);
            }

        }
        private async void BGetAllPages_Click(object sender, RoutedEventArgs e)
        {
            int min = IsIntValid(txtboxMin.Text);
            int max = IsIntValid(txtboxMax.Text);
            if (max == -1)
                return;
            GetLoginPassword();
            if (UserLogin == null || UserPassword == null)
            {
                MessageBox.Show("Заполните поля login и пароль"); return;
            }

            else
            {
                await Write_to_DB(min, max);
                return;
            }
        }
        private List<string> GetNewLinksFromPageByPage(int min, int max)
        {
            var db_girls_links = GetDBLinks();
            if (!string.IsNullOrWhiteSpace(NewHost))
            {
                db_girls_links = db_girls_links.Select(x => Regex.Replace(x, @":\/\/\w+-\w+\.\w+\/", "://" + NewHost + "/")).ToList();
            }
            var outlist = new List<string>();
            Parallel.For(min, max + 1, i =>
            {
                using (WebClient wc = new WebClient())
                {
                    var hDoc = new HtmlDocument();
                    hDoc.LoadHtml(wc.DownloadString($"{Host}/page/{i}/"));
                    var html = hDoc.DocumentNode.SelectSingleNode("/html").OuterHtml;
                    outlist.Add(html);
                }


            });
            var hDoc1 = new HtmlDocument();
            hDoc1.LoadHtml(string.Join("\n", outlist));
            var nodes1 = hDoc1.DocumentNode.SelectNodes(".//*[@id=\"dle-content\"]/*[@class=\"sep \"]/a");
            var nodes2 = hDoc1.DocumentNode.SelectNodes(".//*[@id=\"dle-content\"]/*[@class=\"sep newstory\"]/a");
            var list1 = ReturnNodes(nodes1);
            var list2 = ReturnNodes(nodes2);
            var list = list1.Concat(list2).ToList().Except(db_girls_links);
            return list.ToList();
        }
        private IEnumerable<string> ReturnNodes(HtmlNodeCollection nodes)
        {
            if (nodes == null)
                return new List<string>();
            else
                return nodes.Cast<HtmlNode>().Select(x => x.GetAttributeValue("href", "SHIT").Replace("https", "http"));
        }
        private void DB_Write_Procedure(Girl girl)
        {
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = "insert into Test(name, birthdate,city,adddate,socnet,images,agethen,videos,dateofstate,linkid,link,birthdatestr,views,rating,ava,lastUpdate,lastAccess) " +
                          "values( @name ,@birthdate, @city, @adddate,@socnet, @images,@agethen, @videos, @dateofstate, @linkid, @link,@birthdatestr,@views,@rating,@ava,@lastUpdate,@lastAccess)";
                    CMD.Parameters.Add("@name", System.Data.DbType.String).Value = girl.Name;
                    CMD.Parameters.Add("@birthdate", System.Data.DbType.String).Value = girl.BirthDate.ToString("yyyy-MM-dd");
                    CMD.Parameters.Add("@city", System.Data.DbType.String).Value = girl.City;
                    CMD.Parameters.Add("@agethen", System.Data.DbType.Decimal).Value = girl.AgeThen;
                    CMD.Parameters.Add("@adddate", System.Data.DbType.String).Value = girl.AddDate.ToString("yyyy-MM-dd");
                    CMD.Parameters.Add("@socnet", System.Data.DbType.String).Value = girl.Socials;
                    CMD.Parameters.Add("@images", System.Data.DbType.String).Value = string.Join("\n", girl.Images);
                    CMD.Parameters.Add("@videos", System.Data.DbType.String).Value = string.Join("\n", girl.Videos);
                    CMD.Parameters.Add("@dateofstate", System.Data.DbType.String).Value = girl.DateOfState.ToString("yyyy-MM-dd");
                    CMD.Parameters.Add("@linkid", System.Data.DbType.Int32).Value = girl.LinkId;
                    CMD.Parameters.Add("@link", System.Data.DbType.String).Value = girl.Link;
                    CMD.Parameters.Add("@birthdatestr", System.Data.DbType.String).Value = girl.BirthDateAsIs;
                    CMD.Parameters.Add("@views", System.Data.DbType.Int32).Value = girl.Views;
                    CMD.Parameters.Add("@rating", System.Data.DbType.Int32).Value = girl.Rating;
                    CMD.Parameters.Add("@ava", System.Data.DbType.String).Value = girl.Ava;
                    CMD.Parameters.Add("@lastUpdate", System.Data.DbType.String).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    CMD.Parameters.Add("@lastAccess", System.Data.DbType.String).Value = DateTime.Now.ToString("yyyy-MM-dd");
                    CMD.ExecuteNonQuery();
                }
            }
        }
        private void WriteToOpenDB(Girl girl, bool updated = false)
        {
            SQLiteCommand CMD;
            if (!updated)
            {
                CMD = DB.CreateCommand();

                CMD.CommandText = $"update test " +
                    $"set " +
                    $"views = '{girl.Views}', " +
                    $"rating = '{girl.Rating}', " +
                    $"lastAccess = '{DateTime.Now.ToString("yyyy-MM-dd")}' " +
                    $"where link == '{girl.Link}'";
                CMD.ExecuteNonQuery();
                Debug.WriteLine("WriteToOpenDB 1 " + girl.Link);
                return;
            }
            CMD = DB.CreateCommand();

            CMD.CommandText = $"update test " +
                $"set " +
                $"lastUpdate = '{DateTime.Now:yyyy-MM-dd}', " +
                    $"lastAccess = '{DateTime.Now:yyyy-MM-dd}', " +
                $"images = '{string.Join("\n", girl.Images.Select(x => x.Replace("https", "http")).ToList())}'," +
                $"videos = '{string.Join("\n", girl.Videos.Select(x => x.Replace("https", "http")).ToList())}'," +
                $"views = '{girl.Views}', " +
                $"rating = '{girl.Rating}', " +
                $"name = '{girl.Name}', " +
                $"birthdate = '{girl.BirthDate:yyyy-MM-dd}', " +
                $"city = '{girl.City}', " +
                $"agethen = '{girl.AgeThen}', " +
                $"adddate = '{girl.AddDate:yyyy-MM-dd}', " +
                $"birthdatestr = '{girl.BirthDateAsIs}', " +
                $"ava = '{girl.Ava}' " +
                $"where link == '{girl.Link}'";
            CMD.ExecuteNonQuery();
            Debug.WriteLine("WriteToOpenDB 2 " + girl.Link);

        }
        private async Task Write_to_DB(int min, int max)
        {
            await Task.Run(() =>
            {
                Stopwatch stw = new Stopwatch();
                stw.Start();
                List<string> newcomers = GetNewLinksFromPageByPage(min, max);
                stw.Stop();
                if (newcomers.Count == 0)
                    Dispatcher.Invoke(() => tBlock1.Text = $"Новых нет! за {stw.ElapsedMilliseconds / 1000.0} с.");
                else
                {
                    Dispatcher.Invoke(() => tBlock1.Text = $"{newcomers.Count} новых за {stw.ElapsedMilliseconds / 1000.0} с.");
                    if (!Login(true).Result)
                        return;
                    else
                    {
                        int i = 1;
                        newcomers.ForEach((l) =>
                        {
                            if (i % 500 == 0)
                            {
                                Thread.Sleep(30000);
                            }
                            Girl girl = To_list(l).Result;

                            DB_Write_Procedure(girl);
                            i++;
                        });
                    }

                }
            });
        }
        private string DTime_Now_For_File()
        {
            return Regex.Replace(DateTime.Now.ToLongTimeString(), @"\D", "_");
        }
        private async Task<bool> Login(bool useHttpClient = true)
        {
            Uri uri = new Uri(Host);
            List<string> keys = new List<string>();
            HtmlDocument hDoc = new HtmlDocument();
            foreach (Cookie c in _cc)
                container.Add(new Cookie(c.Name, c.Value, "/", $".{Regex.Replace(Host, @"https?:\/\/", "")}"));
            HttpResponseMessage response = await httpClient.GetAsync(uri);
            IEnumerable<Cookie> cookies = container.GetCookies(uri).Cast<Cookie>();
            if (cookies.Any(x => x.Name == "dle_user_id"))
                return true;
            foreach (Cookie c in cookies)
                _cc.Add(c);
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            Encoding encoding = Encoding.GetEncoding("windows-1251");
            string responseString = encoding.GetString(bytes, 0, bytes.Length);
            hDoc.LoadHtml(responseString);
            keys.Add(hDoc.DocumentNode.SelectSingleNode("//*[@id=\"myModal\"]/div[2]/form/input[3]").GetAttributeValue("value", "SHIT"));
            keys.Add(hDoc.DocumentNode.SelectSingleNode("//*[@id=\"myModal\"]/div[2]/form/input[4]").GetAttributeValue("value", "SHIT"));
            keys.Add(hDoc.DocumentNode.SelectSingleNode("//*[@id=\"myModal\"]/div[2]/form/input[5]").GetAttributeValue("value", "SHIT"));
            keys.Add(hDoc.DocumentNode.SelectSingleNode("//*[@id=\"myModal\"]/div[2]/form/input[6]").GetAttributeValue("value", "SHIT"));
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("login_name", Dispatcher.Invoke(()=>UserLogin)),
                new KeyValuePair<string, string>("login_password",Dispatcher.Invoke(()=>UserPassword)),
                new KeyValuePair<string, string>("sitekey",keys[0]),
                new KeyValuePair<string, string>("keyz",keys[1]),
                new KeyValuePair<string, string>("logkey",keys[2]),
                new KeyValuePair<string, string>("fkeyz",keys[3]),
                new KeyValuePair<string, string>("login","submit")
            });
            HttpResponseMessage login_response = await httpClient.PostAsync("", content);
            cookies = container.GetCookies(uri).Cast<Cookie>();
            foreach (Cookie c in cookies)
                _cc.Add(c);
            return login_response.StatusCode == HttpStatusCode.OK;
        }

        public DateTime Date_Parser(string date_str)
        {
            try
            {
                if (DateTime.TryParse(date_str, out DateTime dateValue))
                    return dateValue;
                else
                    return DateTime.MinValue;
            }
            catch (Exception e)
            {
                return DateTime.MinValue;
            }
        }
        private decimal Get_Age(string later_date, string earlier_date)
        {
            if (Convert.ToDateTime(earlier_date) != DateTime.MinValue)
                return Math.Floor(((decimal)(Convert.ToDateTime(later_date) - Convert.ToDateTime(earlier_date)).Days / 365));
            else
                return Math.Floor((decimal)(Convert.ToDateTime(earlier_date) - Convert.ToDateTime(earlier_date)).Days / 365);
        }
        private decimal Get_Age(string stringAge)
        {
            if (decimal.TryParse(stringAge, out decimal result))
                return result;
            else
                return 0.0m;
        }
        private decimal Get_Age(DateTime later_date, DateTime earlier_date)
        {
            if (Convert.ToDateTime(earlier_date) != DateTime.MinValue)
                return Math.Floor(((decimal)(later_date - earlier_date).Days / 365));
            else
                return Math.Floor((decimal)(Convert.ToDateTime(earlier_date) - Convert.ToDateTime(earlier_date)).Days / 365);
        }
        private async Task<Girl> To_list(string girlLink)
        {
            Girl girl = new Girl();
            try
            {
                HtmlDocument htmldoc = await Page_parsing(girlLink, true);//TODO asynchronously
                var add_date = htmldoc.DocumentNode.SelectSingleNode("//*[@id=\"dle-content\"]/span/a[1]").InnerText;
                var fullinfo_nodes = htmldoc.DocumentNode.SelectNodes("//*[@id=\"dle-content\"]/div[5]/div[2]/ul/li");
                var fullinfo_li = fullinfo_nodes.Cast<HtmlNode>().Select(x => x.InnerText.Trim()).ToList();
                var fullpost_nodes = htmldoc.DocumentNode.SelectNodes("//*[@id=\"dle-content\"]/div[5]/div[3]/a");
                var fullpost_hrefs = fullpost_nodes.Cast<HtmlNode>().Select(x => x.GetAttributeValue("href", "SHIT")).Select(x => Host + x).ToList();
                var ava = Host + htmldoc.DocumentNode.SelectSingleNode("//*[@id=\"dle-content\"]/div[5]/div[1]/img").GetAttributeValue("src", "SHIT");
                int views = Convert.ToInt32(Regex.Match(htmldoc.DocumentNode.SelectSingleNode("//*[@id=\"dle-content\"]/span").InnerText,
                    @"Просмотров:(\s+(\d{1,8}))").Groups[2].Value);
                HtmlNode rating_node = htmldoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div/div[1]/div/div[3]/div/ul/li[1]");
                int rating = rating_node == null ? 0 : Rating_Int(rating_node.InnerText);
                List<string> srcs = new List<string>();
                var vvideo_nodes = htmldoc.DocumentNode.SelectNodes("//*[@id=\"dle-content\"]/div[5]/div[4]/div/div/video/source");
                if (vvideo_nodes != null && vvideo_nodes.Count != 0)
                {
                    for (int i = 1; i <= vvideo_nodes.Count(); i++)
                    {
                        try
                        {
                            srcs.Add(Host + htmldoc.DocumentNode.SelectSingleNode($"//*[@id=\"dle-content\"]/div[5]/div[4]/div[{i}]/div/video/source").GetAttributeValue("src", "SHIT"));
                        }
                        catch (Exception exc)
                        {

                        }
                    }
                }
                girl.Name = fullinfo_li[0].ToUpper();
                girl.BirthDateAsIs = fullinfo_li[1].Replace("Возраст:", "").Trim();
                girl.BirthDate = Date_Parser(girl.BirthDateAsIs);
                girl.City = fullinfo_li[2].Replace("Город:", "").Trim().ToUpper();
                girl.Socials = fullinfo_li.Count() > 3 ? string.Join("\n",
                fullinfo_li.GetRange(3, fullinfo_li.Count() - 3)) : null;
                girl.DateOfState = DateTime.Now;
                girl.AddDate = Date_Parser(add_date);
                if (girl.BirthDateAsIs.Length == 2)
                    girl.AgeThen = Get_Age(girl.BirthDateAsIs);
                else
                    girl.AgeThen = Get_Age(girl.AddDate, girl.BirthDate);
                girl.Images = fullpost_hrefs;
                girl.Videos = srcs;
                girl.LinkId = Convert.ToInt32(Regex.Match(girlLink, @"\/(\d{1,})-").Groups[1].Value);
                girl.Link = girlLink;
                girl.Ava = ava;
                girl.Views = views;
                girl.Rating = rating;

                return girl;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return girl;
            }
        }
        private int Rating_Int(string s)
        {
            if (int.TryParse(s, out int result))
                return result;
            else
                return 0;
        }
        private async Task<HtmlDocument> Page_parsing(string url, bool usinghttpclient = true)
        {
            url = Regex.Replace(url, @"(https?:\/\/)[^/]+\/", $"$1{NewHost}/");
            foreach (Cookie c in _cc)
                handler.CookieContainer.Add(new Cookie(c.Name, c.Value, "/", $".{Regex.Replace(Host, @"https?:\/\/", "")}"));
            HttpResponseMessage response = await httpClient.GetAsync(url);
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            Encoding encoding = Encoding.GetEncoding("windows-1251");
            string responseString = encoding.GetString(bytes, 0, bytes.Length);
            HtmlDocument hDoc = new HtmlDocument();
            hDoc.LoadHtml(responseString);
            return hDoc;
        }

        private string FolderPath()
        {
            return @"htmls\";
        }
        private string Video_Section(Girl girl)
        {
            var state = GetGirlDownloadState();
            string girlFolder = Path.GetFileNameWithoutExtension(girl.Link);
            string _video_ouput = "", video_section = "";
            if (girl.Videos != null && girl.Videos.Count() > 0 && girl.Videos[0] != "")//ПРОВЕРЯЕМ УСЛОВИЕ: ЧТО ТАМ ПО ВИДЕО ЕСТЬ ИЛИ НЕТ?
            {
                for (int i = 0; i < girl.Videos.Count(); i++)
                {
                    string a;
                    if (state[girl.Link])
                        a = Path.Combine(Path.Combine(RootDatabaseFileFolder, girlFolder, "videos", Path.GetFileName(girl.Videos[i])));
                    else
                        a = girl.Videos[i];
                    _video_ouput += $"<div class=\"container\" style=\"margin-top:10px;\">" +
                        $"<video width=\"1250\" height=\"750\" controls=\"\"><source src=" +
                        $"\"{a}\" type=" +
                        $"\"video/mp4\"></video></div>\"";
                }

                video_section = $"<br><details><summary>Раскрыть видео</summary><br>{_video_ouput}</details>";
            }
            return video_section;

        }
        private string MainFolderPath { get; set; }
        private void MakeHtml(Girl girl)
        {
            var state = GetGirlDownloadState();
            string girlFolder = Path.GetFileNameWithoutExtension(girl.Link);
            Regex pat = new Regex(@"https?:\/\/\S+?\/(\S+?)\/(\S+?html)");
            Match match = pat.Match(girl.Link);
            string fileName = $"{match.Groups[1].Value}_{match.Groups[2].Value}";
            string file_path = $@"{MainFolderPath}{fileName}";
            //if (File.Exists(file_path))
            //{
            //    MessageBox.Show("File Exists!");
            //    Process.Start(file_path);
            //    return;
            //}
            string info = $"<br><table><tr><td colspan=\"2\">{girl.Name}</td></tr><tr><td>Возраст:</td><td>{girl.AgeThen}</td></tr><tr>" +
                $"<td>Город:</td><td>{girl.City}</td></tr><tr><td>Дата Рождения</td><td>{girl.BirthDate}</td></tr><tr><td>Дата добавления:</td><td>{girl.AddDate}</td></tr><tr>" +
                $"<td colspan=\"2\" style=\"text-align: center;\"><a href=\"{girl.Link}\" target=\"_blank\">{girl.Name}</a></td></tr></table>";
            string image_section_start = "<details><summary>Раскрыть фото</summary><div class=\"demo-gallery\"><ul id = \"lightgallery\" class=\"list-unstyled row\">";
            string last_line = "</ul></div></details>";

            string image_output = "";
            for (int i = girl.Images.Count() - 1; i >= 0; i--)
            {
                string a;
                if (state[girl.Link])
                    a = Path.Combine(Path.Combine(RootDatabaseFileFolder, girlFolder, "images", Path.GetFileName(girl.Images[i])));
                else
                    a = girl.Images[i];
                string base_html = $"<li class=\"col-xs-6 col-sm-4 col-md-2 col-lg-2\" data-responsive=\"{a}\"" +
                $"data-src=\"{a}\"data-sub-html=\"\"><a href=\"\"><img class=\"img-responsive\" " +
                $"src=\"{a}\"></a></li>";
                image_output += base_html;
            }
            string video_section = Video_Section(girl);
            string output = info + video_section + image_section_start + image_output + last_line;
            string test_html = File.ReadAllText($"{MainFolderPath}template.html");
            string temp_html = test_html.Replace("<title></title>", $"<title>{girl.Name}</title>");
            temp_html = temp_html.Replace("<h2></h2>", $"<h2><a href=\"{girl.Link}\"  target=\"_blank\">{girl.Name}</a></h2>{output}");
            File.WriteAllText(file_path, temp_html);
            Process.Start(file_path);
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Environment.CurrentDirectory);
            FileInfo[] fileInfo = directoryInfo.GetFiles("*.dpl");
            foreach (var fileinfo in fileInfo)
            {
                File.Delete(fileinfo.FullName);
            }
        }

        private void GetVideoLinksFromDB()
        {
            VideoLinksToFile.IsEnabled = false;
            VideoGirls videoGirls = YamlDeSerialize();
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = "select link, videos from Test";
                    using (SQLiteDataReader SQL = CMD.ExecuteReader())
                    {
                        if (SQL.HasRows)
                        {
                            while (SQL.Read())
                            {
                                VideoProperties properties = new VideoProperties();
                                string temp = (string)SQL["link"];
                                if (videoGirls.Properties.Any(x => x.GirlPage == temp))
                                {
                                    properties = videoGirls.Properties.Where(x => x.GirlPage == temp).First();
                                    var item = videoGirls.Properties.FirstOrDefault(x => x.GirlPage == temp);
                                    videoGirls.Properties.Remove(item);
                                    ((string)SQL["videos"]).Split('\n').ToList().ForEach(x =>
                                    {
                                        bool flag = false;
                                        foreach (var video in properties.Videos)
                                        {
                                            var mx = Regex.Match(x, @"\/video\d?\/(.+?.mp4)").Groups[1].Value;
                                            var murl = Regex.Match(video.Url, @"\/video\d?\/(.+?.mp4)").Groups[1].Value;
                                            if (mx == murl)
                                            {
                                                flag = true;
                                                break;
                                            }

                                        }
                                        if (!flag)
                                        {
                                            properties.Videos.Add(new Video() { Url = x });

                                            File.AppendAllText(ManageDataGrids.UpdatedVideos, temp);
                                        }
                                    });
                                }
                                else
                                {
                                    properties.GirlPage = temp;
                                    ((string)SQL["videos"]).Split('\n').ToList().ForEach(x =>
                                    {
                                        properties.Videos.Add(new Video() { Url = x });
                                    });
                                }

                                videoGirls.Properties.Add(properties);
                            }
                        }
                    }
                }
            }
            YamlSerialize(videoGirls);
            VideoLinksToFile.IsEnabled = true;
        }
        bool breaker;
        private async Task<Video> UpdateVideoInfo(string videoLink)
        {
            Video video = new Video();
            string[] result = await GetVideoProperties(videoLink);
            video.Url = videoLink;
            video.Duration = result[0];
            video.Size = result[1];
            video.Quality = result[2];
            return video;
        }
        private async void MoveToSQL_Click(object sender, RoutedEventArgs e)
        {
            return;
            await MoveFromYamlToSql();
        }

        private async Task MoveFromYamlToSql()
        {
            string fromFile = File.ReadAllText(ManageDataGrids.VideoGirlsPath);
            VideoGirls videoGirls = YamlDeSerialize(fromFile);
            string command = "select * from test";
            var girlList = ManageDataGrids.ReturnGirlsInfo(command);
            foreach (var girl in girlList.ListOfGirls)
            {
                var props = videoGirls.Properties.Where(x => x.GirlPage == girl.Link).ToList();
                foreach (var prop in props)
                {
                    foreach (var video in prop.Videos)
                        WriteVideoToSQL(girl.Id, video);
                }
            }
        }
        private void CheckVideoInTable()
        {
            List<long> ids = new List<long>();
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = "select id from Test";
                    using (SQLiteDataReader SQL = CMD.ExecuteReader())
                    {
                        if (SQL.HasRows)
                        {
                            while (SQL.Read())
                            {
                                ids.Add(SQL.GetInt64(0));
                            }
                        }
                    }
                }
            }
            List<long> girlIds = new List<long>();
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = "select girl_id from videos";
                    using (SQLiteDataReader SQL = CMD.ExecuteReader())
                    {
                        if (SQL.HasRows)
                        {
                            while (SQL.Read())
                            {
                                girlIds.Add((long)SQL["girl_id"]);
                            }
                        }
                    }
                }
            }
            List<long> newIds = ids.Except(girlIds).ToList();
            foreach (long id in newIds)
            {
                List<Video> videos = new List<Video>();
                using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
                {
                    DB.Open();
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = $"select videos from test where id == {id}";
                        using (SQLiteDataReader SQL = CMD.ExecuteReader())
                        {
                            if (SQL.HasRows)
                            {
                                while (SQL.Read())
                                {
                                    string vidString = (string)SQL["videos"];
                                    if (!string.IsNullOrEmpty(vidString))
                                        vidString.Split('\n').ForEach(x => videos.Add(new Video() { Url = x, girlId = id }));
                                    else
                                        videos.Add(new Video() { Url = vidString, girlId = id });
                                }
                            }
                        }
                    }
                }
                videos.ForEach(x => WriteVideoToSQL(id, x));
            }
        }

        private void WriteVideoToSQL(long girlId, Video video)
        {
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = "insert into videos(url,filename,size,quality,duration,girl_id) " +
                          "values(@url,@filename,@size,@quality,@duration,@girl_id)";
                    CMD.Parameters.Add("@url", System.Data.DbType.String).Value = video.Url;
                    CMD.Parameters.Add("@filename", System.Data.DbType.String).Value = video.Filename;
                    CMD.Parameters.Add("@size", System.Data.DbType.String).Value = video.Size;
                    CMD.Parameters.Add("@quality", System.Data.DbType.String).Value = video.Quality;
                    CMD.Parameters.Add("@duration", System.Data.DbType.String).Value = video.Duration;
                    CMD.Parameters.Add("@girl_id", System.Data.DbType.Int64).Value = girlId;
                    CMD.ExecuteNonQuery();
                }
            }
        }
        private async Task GetVideosInfo()
        {
            breaker = false;
            string fromFile = File.ReadAllText(ManageDataGrids.VideoGirlsPath);
            VideoGirls videoGirls = YamlDeSerialize(fromFile);
            int i = 0;
            try
            {
                foreach (var property in videoGirls.Properties)
                {
                    i++;
                    Dispatcher.Invoke(() => tBlockId.Text = Path.GetFileNameWithoutExtension(property.GirlPage));/*+Regex.Match(property.GirlPage, @"\/(\d+)-").Groups[1].Value)*/;
                    Dispatcher.Invoke(() => tBlockCurrentId.Text = $"{i}/{videoGirls.Properties.Count}");
                    if (property.Videos == null || property.Videos.Count == 0)
                        continue;
                    foreach (var video in property.Videos)
                    {
                        if (string.IsNullOrEmpty(video.Url))
                            continue;
                        if (!string.IsNullOrEmpty(video.Duration))
                            continue;
                        string[] result = await GetVideoProperties(video.Url, property.GirlPage);
                        video.Duration = result[0];
                        video.Size = result[1];
                        video.Quality = result[2];
                    }
                    if (breaker)
                        break;


                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            finally
            {
                YamlSerialize(videoGirls);
                if (breaker)
                    Dispatcher.Invoke(() => tBlock1.Text = $"Прервано");
                else
                    Dispatcher.Invoke(() => tBlock1.Text = $"Завершено");
                breaker = false;
                GetVideosInfos.IsEnabled = true;
            }
        }
        private async Task<string[]> GetVideoProperties(string url)
        {
            try
            {
                IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(url);
                int width = mediaInfo.VideoStreams.Select(x => x.Width).First();
                int heigth = mediaInfo.VideoStreams.Select(x => x.Height).First();
                int quality = width > heigth ? width : heigth;
                return new string[] { mediaInfo.Duration.ToString(), mediaInfo.Size.ToString(), quality.ToString() };
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.Message);
                return new string[] { "", "", "" };
            }
        }
        private async Task<string[]> GetVideoProperties(string url, string GirlPage)
        {
            try
            {


                string girlFolder = Path.GetFileNameWithoutExtension(GirlPage);
                string filePath = Path.Combine(RootDatabaseFileFolder, girlFolder, "videos", Path.GetFileName(url));
                IMediaInfo mediaInfo;
                if (File.Exists(filePath))
                    mediaInfo = await FFmpeg.GetMediaInfo(filePath);
                else
                    mediaInfo = await FFmpeg.GetMediaInfo(url);
                int width = mediaInfo.VideoStreams.Select(x => x.Width).First();
                int heigth = mediaInfo.VideoStreams.Select(x => x.Height).First();
                int quality = width > heigth ? width : heigth;
                return new string[] { mediaInfo.Duration.ToString(), mediaInfo.Size.ToString(), quality.ToString() };
            }
            catch
            {
                return new string[] { "", "", "" };
            }
        }
        private void YamlSerialize(VideoGirls videoGirls)
        {
            var serializer = new SerializerBuilder().Build();
            File.WriteAllText(ManageDataGrids.VideoGirlsPath, serializer.Serialize(videoGirls));
        }
        private VideoGirls YamlDeSerialize(string stringToDeserialize)
        {
            var deserializer = new DeserializerBuilder().Build();
            return deserializer.Deserialize<VideoGirls>(stringToDeserialize);
        }
        private VideoGirls YamlDeSerialize()
        {
            string fromFile = File.ReadAllText(ManageDataGrids.VideoGirlsPath);
            var deserializer = new DeserializerBuilder().Build();
            return deserializer.Deserialize<VideoGirls>(fromFile);
        }

        private void VideoLinksToFile_Click(object sender, RoutedEventArgs e)
        {
            CheckVideoInTable();
            return;
            GetVideoLinksFromDB();
        }

        private async void GetVideosInfos_Click(object sender, RoutedEventArgs e)
        {
            return;
            GetVideosInfos.IsEnabled = false;
            await GetVideosInfo();
        }

        private void Breaker_Click(object sender, RoutedEventArgs e)
        {
            breaker = true;
        }
        private void MakeCommand()
        {
            string command = $@"select * from Test 
where (link like '%{tBox43.Text}%') and 
(agethen BETWEEN {AgeBetween(tBox44.Text).Item1} and {AgeBetween(tBox44.Text).Item2}) and 
(birthdate BETWEEN {YearsBetween(tBox42.Text).Item1} and {YearsBetween(tBox42.Text).Item2}) and 
(rating BETWEEN {(int.TryParse(tBox46.Text, out int x) ? x : 0)} and {(int.TryParse(tBox46.Text, out x) ? x : 100)}) and 
(city like '%{tBox45.Text.ToUpper()}%') and (name like '%{tBox41.Text.ToUpper()}%') {NotesToQuery()}";
            tBox411.Text = command;
        }
        private string NotesToQuery()
        {
            if ((tBox413.Text) == "null")
                return $" and ((notes like '') or(notes is null))";
            if (string.IsNullOrWhiteSpace(tBox413.Text))
                return $" and ((notes like '%%') or(notes is null))";
            string[] notes = tBox413.Text.Trim().Split(' ');
            StringBuilder sb = new StringBuilder();
            //sb.Append(" and (");
            foreach (string note in notes)
            {
                sb.Append($"and (notes like '%{note.ToUpper()}%')");
            }
            //sb.Append(")");
            return sb.ToString();
        }
        private Tuple<int, int> AgeBetween(string str)
        {
            Tuple<int, int> output = new Tuple<int, int>(-100, 100);
            if (string.IsNullOrEmpty(str))
                return output;
            if (!str.Contains(":"))
            {
                if (int.TryParse(str, out int result))
                    return new Tuple<int, int>(result, result);
                return output;
            }
            else if (str.StartsWith(":"))
                return new Tuple<int, int>(-100, ParseInt(str.Replace(":", "")));
            else if (str.EndsWith(":"))
                return new Tuple<int, int>(ParseInt(str.Replace(":", "")), 100);
            else
            {
                string[] temp = str.Split(':');
                return new Tuple<int, int>(ParseInt(temp[0]), ParseInt(temp[1]));
            }
        }
        private int ParseInt(string str)
        {
            if (int.TryParse(str, out int result))
                return result;
            else
                return 0;
        }
        private Tuple<int, int> YearsBetween(string str)
        {
            Tuple<int, int> output = new Tuple<int, int>(0, 2100);
            if (string.IsNullOrEmpty(str))
                return output;
            if (!str.Contains(":"))
            {
                if (int.TryParse(str, out int result))
                {
                    int outResult = PlusCenturies(result);
                    return new Tuple<int, int>(outResult, outResult + 1);
                }
                return output;
            }
            else if (str.StartsWith(":"))
                return new Tuple<int, int>(0, PlusCenturies(str.Replace(":", "")) + 1);
            else if (str.EndsWith(":"))
                return new Tuple<int, int>(PlusCenturies(str.Replace(":", "")), 2100);
            else
            {
                string[] temp = str.Split(':');
                return new Tuple<int, int>(PlusCenturies(temp[0]), PlusCenturies(temp[1]) + 1);
            }
        }
        private int PlusCenturies(string str)
        {
            if (int.TryParse(str, out int year))
                if (year > 50 && year <= 99)
                    year += 1900;
                else if (year >= 0 && year <= 49)
                {
                    year += 2000;
                }
            return year;

        }
        private int PlusCenturies(int year)
        {
            if (year > 50 && year <= 99)
                year += 1900;
            else if (year >= 0 && year <= 49)
            {
                year += 2000;
            }
            return year;

        }
        private void tBox411_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleTheSortingFields();
            }
        }
        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyType == typeof(System.DateTime))
                (e.Column as DataGridTextColumn).Binding.StringFormat = "dd.MM.yyyy";
        }
        private bool CompareDates(DateTime date1, DateTime date2)
        {
            if (DateTime.TryParse(tBox412.Text, out DateTime result))
                return true;
            //Math.Abs((new DateTime(2020, date1.Month, date1.Day) - new DateTime(2020,result.Month,result.Day)).Days)<=1;
            else if (int.TryParse(tBox412.Text, out int intResult))
            {
                int leapYears = 0;
                for (int y = date1.Year; y < date2.Year; y++)
                {
                    if (DateTime.IsLeapYear(y))
                    {
                        if (y == date1.Year)
                            if (date1 < new DateTime(date1.Year, 3, 1))
                                leapYears++;
                            else
                                continue;
                        else
                            leapYears++;
                    }
                }
                if (DateTime.IsLeapYear(date2.Year) && date2 >= new DateTime(date2.Year, 3, 1))
                    leapYears++;

                return Math.Abs((date2 - date1).Days % 365 - leapYears) <= intResult;
            }
            else return true;
        }
        private List<Girl> GetInfoByVideoProps()
        {
            var pickedGirls = ManageDataGrids.ReturnGirlsInfo(tBox411.Text).ListOfGirls
                .Where(x => CompareDates(x.BirthDate, x.AddDate)).ToList();

            var ids = pickedGirls.Select(x => x.Id);
            List<Video> videos = GetVideosFromSQL();
            List<Video> pickedVideos = new List<Video>();
            IEnumerable<Video> pickedVideosTemp;
            foreach (long id in ids)
            {
                var temp = videos.Where(x => x.girlId == id).FirstOrDefault();
                if (temp != null)
                    pickedVideos.Add(temp);
            }
            int quality = int.TryParse(tBox48.Text, out quality) ? quality : 0;
            long size = long.TryParse(tBox47.Text, out size) ? size : 0;
            TimeSpan duration = TimeSpan.FromMinutes(double.TryParse(Regex.Replace(tBox49.Text, @"[><=]{1}", ""), out double res) ? res : 0);
            //int.TryParse(Regex.Replace(tBox410.Text, @"[><=]{1}", ""), out int count);
            (int, int) desiredRange = DesiredCount(Dispatcher.Invoke(() => tBox410.Text));
            IEnumerable<long> hasAnyVids;
            pickedVideosTemp = pickedVideos.Where(y => ((int.TryParse(y.Quality, out int intQuality) ? intQuality : 0) >= quality));
            if (withThumbnail.IsChecked == true)
                pickedVideosTemp = pickedVideosTemp.Where(x => !string.IsNullOrWhiteSpace(x.Url) && File.Exists(Path.Combine("E:\\My_thumbnails", Path.GetFileName(x.Url) + ".jpg")));
            hasAnyVids = pickedVideosTemp.Select(x=>x.girlId).ToList();
            List<Girl> girls = new List<Girl>();
            var state = GetGirlDownloadState();
            foreach (long id in hasAnyVids)
            {
                var temp = pickedGirls.Where(x => x.Id == id).FirstOrDefault();
                if (temp != null)
                    if (Offline.IsChecked == false)
                        girls.Add(temp);
                    else if (state[temp.Link])
                        girls.Add(temp);
            }
            return girls.OrderByDescending(x => x.LastUpdate).ToList();

        }
        //private List<Girl> GetInfoByVideoProps()
        //{
        //    var pickedGirls = ManageDataGrids.ReturnGirlsInfo(tBox411.Text).ListOfGirls
        //        .Where(x => CompareDates(x.BirthDate, x.AddDate)).ToList();

        //    var links = pickedGirls.Select(x => x.Link);
        //    VideoGirls videoGirls = YamlDeSerialize();
        //    List<VideoProperties> pickedVideoInfos = new List<VideoProperties>();
        //    IEnumerable<VideoProperties> filteredVideoInfos;
        //    foreach (string link in links)
        //    {
        //        var temp = videoGirls.Properties.Where(x => x.GirlPage == link).FirstOrDefault();
        //        if (temp != null)
        //            pickedVideoInfos.Add(temp);
        //    }
        //    int quality = int.TryParse(tBox48.Text, out quality) ? quality : 0;
        //    long size = long.TryParse(tBox47.Text, out size) ? size : 0;
        //    TimeSpan duration = TimeSpan.FromMinutes(double.TryParse(Regex.Replace(tBox49.Text, @"[><=]{1}", ""), out double res) ? res : 0);
        //    //int.TryParse(Regex.Replace(tBox410.Text, @"[><=]{1}", ""), out int count);
        //    (int, int) desiredRange = DesiredCount(Dispatcher.Invoke(() => tBox410.Text));
        //    IEnumerable<string> hasAnyVids;
        //    if (desiredRange.Item1 == 0 && desiredRange.Item2 == 0)
        //    {
        //        hasAnyVids = pickedVideoInfos.Where(x => x.Videos.Count == 1 && string.IsNullOrEmpty(x.Videos[0].Url)).Select(x => x.GirlPage);
        //    }
        //    else if (desiredRange.Item1 == 0)
        //    {
        //        hasAnyVids = pickedVideoInfos.Select(x => x.GirlPage);
        //    }
        //    else
        //    {
        //        if (withThumbnail.IsChecked == true)
        //            filteredVideoInfos = pickedVideoInfos.Where(x => !string.IsNullOrWhiteSpace(x.Videos[0].Url) && File.Exists(Path.Combine("E:\\My_thumbnails", Path.GetFileName(x.Videos[0].Url) + ".jpg")) && IsVideosCountInRange(desiredRange, x.Videos.Count));
        //        else
        //            filteredVideoInfos = pickedVideoInfos.Where(x => !string.IsNullOrWhiteSpace(x.Videos[0].Url) && !File.Exists(Path.Combine("E:\\My_thumbnails", Path.GetFileName(x.Videos[0].Url) + ".jpg")) && IsVideosCountInRange(desiredRange, x.Videos.Count));
        //        hasAnyVids = filteredVideoInfos.Where(x => x.Videos.Where(y => ((int.TryParse(y.Quality, out int intQuality) ? intQuality : 0) >= quality) &&
        //GetDurationRange(y.Quality, tBox49.Text, y.Duration))
        //.Count() > 0).Select(x => x.GirlPage);
        //    }
        //    List<Girl> girls = new List<Girl>();
        //    var state = GetGirlDownloadState();
        //    foreach (string page in hasAnyVids)
        //    {
        //        var temp = pickedGirls.Where(x => x.Link == page).FirstOrDefault();
        //        if (temp != null)
        //            if (Offline.IsChecked == false)
        //                girls.Add(temp);
        //            else if (state[temp.Link])
        //                girls.Add(temp);
        //    }
        //    return girls.OrderByDescending(x => x.LastUpdate).ToList();

        //}
        private bool IsVideosCountInRange((int, int) minMax, int girlCount)
        {
            int min, max;
            (min, max) = minMax;
            if (min == int.MaxValue && max == int.MaxValue)
                return false;
            if (girlCount >= min && girlCount <= max)
                return true;
            return false;

        }
        private (int, int) DesiredCount(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt))
                return (0, int.MaxValue);
            Match match;
            if ((match = Regex.Match(txt, @"^(\d+)$")).Success)
            {
                int val = ParseInt(match.Groups[1].Value);
                return (val, val);
            }
            if (!Regex.IsMatch(txt, @"(\d+?)\:(\d+)?"))
                return (0, int.MaxValue);
            match = Regex.Match(txt, @"(\d+)?\:(\d+)?");
            if (!match.Success)
                return (0, int.MaxValue);
            int n1 = ParseInt(match.Groups[1].Value);
            int n2 = match.Groups[2].Value == "" ? int.MaxValue : ParseInt(match.Groups[2].Value);
            if (n1 > n2)
            {
                MessageBox.Show("Неправильньій ввод");
                return (int.MaxValue, int.MaxValue);
            }
            return (n1, n2);
        }
        private bool GetDurationRange(string quality, string tBoxDurationString, string duration)
        {
            if (!string.IsNullOrEmpty(quality))
            {
                if (string.IsNullOrWhiteSpace(tBoxDurationString))
                    return true;
                tBoxDurationString = tBoxDurationString.Replace('.', ',');
                if (TimeSpan.TryParse(duration, out TimeSpan durationConverted))
                {
                    var durationArr = tBoxDurationString.Split(':');
                    if (durationArr.Length == 1)
                    {
                        if (string.IsNullOrWhiteSpace(durationArr[0]))
                            durationArr[0] = "0";
                        if (double.TryParse(durationArr[0], out double minDuration))
                        {
                            if (durationConverted > TimeSpan.FromMinutes(minDuration))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    if (durationArr.Length == 2)
                    {
                        if (string.IsNullOrWhiteSpace(durationArr[0]))
                            durationArr[0] = "0";
                        if (string.IsNullOrWhiteSpace(durationArr[1]))
                            durationArr[1] = "9999";
                        if (double.TryParse(durationArr[0], out double minDuration)
                            && double.TryParse(durationArr[1], out double maxDuration))
                        {
                            if (minDuration == maxDuration)
                                return true;
                            if (durationConverted > TimeSpan.FromMinutes(minDuration) && durationConverted <= TimeSpan.FromMinutes(maxDuration))
                            {
                                return true;
                            }
                            return false;
                        }
                        else
                            return false;
                    }
                }
                return false;
            }
            return false;
        }
        private ComparingSign ReturnComparingSign(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                if (text[0] == '=')
                    return ComparingSign.Equal;
                else if (text[0] == '>')
                    return ComparingSign.GreaterThan;
                else if (text[0] == '<')
                    return ComparingSign.LessThan;
                else
                    return ComparingSign.None;
            }
            else
                return ComparingSign.None;
        }
        /// <summary>
        /// Продолжительность подходящая или нет
        /// </summary>
        /// <returns></returns>
        private bool DurationAgreed(string quality, TimeSpan durationNeeded, string duration, ComparingSign comparingSign)
        {
            if (TimeSpan.TryParse(duration, out TimeSpan durationConverted))
                if (!string.IsNullOrEmpty(quality))
                    if (comparingSign == ComparingSign.GreaterThan && durationConverted > durationNeeded)
                        return true;
                    else if (comparingSign == ComparingSign.LessThan && durationConverted < durationNeeded)
                        return true;
                    else if (comparingSign == ComparingSign.Equal && durationConverted == durationNeeded)
                        return true;
                    else if (comparingSign == ComparingSign.None && durationConverted > durationNeeded)
                        return true;
                    else if (durationConverted == durationNeeded)
                        return true;
            return false;
        }
        private bool Quantity(string quality, ref int count, List<Video> videos, ComparingSign comparingSign)
        {
            if (tBox410.Text == "")
                return true;
            if (count == 0 && (comparingSign == ComparingSign.None || comparingSign == ComparingSign.Equal) && string.IsNullOrEmpty(quality))
                return true;
            if (!string.IsNullOrEmpty(quality))
                if (comparingSign == ComparingSign.GreaterThan && videos.Count > count)
                    return true;
                else if (comparingSign == ComparingSign.LessThan && videos.Count < count)
                    return true;
                else if (comparingSign == ComparingSign.Equal && videos.Count == count)
                    return true;
                else if (videos.Count == count)
                    return true;
            return false;
        }
        enum ComparingSign
        {
            None, LessThan, GreaterThan, Equal
        }
        private void HandleTheSortingFields()
        {
            try
            {
                DataGridGirlsInfo.ItemsSource = GetInfoByVideoProps();
                DataGridGirlsInfo.Columns[1].Width = new DataGridLength(150);
                DataGridGirlsInfo.Columns[2].Width = new DataGridLength(200);
                DataGridGirlsInfo.Columns[3].Visibility = Visibility.Collapsed;
                //DataGridGirlsInfo.Columns[5].Visibility = Visibility.Collapsed;
                //DataGridGirlsInfo.Columns[7].Visibility = Visibility.Collapsed;
                DataGridGirlsInfo.Columns[9].Visibility = Visibility.Collapsed;
                DataGridGirlsInfo.Columns[13].Visibility = Visibility.Collapsed;
                DataGridGirlsInfo.Columns[15].Visibility = Visibility.Collapsed;
                DataGridGirlsInfo.Columns[16].Visibility = Visibility.Collapsed;
                DataGridGirlsInfo.Columns[14].Width = new DataGridLength(150);
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
            //CheckBox[] checkBox = new CheckBox[] { nameCB, birthDateCB, linkCB, ageThenCB, cityCB, ratingCB };            
        }
        private void Enter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MakeCommand();
                HandleTheSortingFields();
            }
        }
        private List<Video> ForPlaylist { get; set; }
        private static string player = @"C:\Program Files\DAUM\PotPlayer\PotPlayerMini64.exe";
        private async void Click_ToPlay(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => TryStartPlaylist(true));
        }
        private void Click_AddToFavorites(object sender, RoutedEventArgs e)
        {
            string favoritesFile = Path.Combine(ManageDataGrids.FilesFolder, "my_favorites.csv");
            if (DataGridGirlsInfo.SelectedItems.Count > 1)
                return;
            List<string> favorites = File.Exists(favoritesFile) ? File.ReadAllLines(favoritesFile).ToList() : new List<string>(); ;
            if ((DataGridGirlsInfo.SelectedItem as Girl) != null)
            {
                var girl = (DataGridGirlsInfo.SelectedItem as Girl);
                if (!favorites.Contains(girl.Link))
                    File.AppendAllText(favoritesFile, girl.Link + "\n");
            }

        }
        private void TryStartPlaylist(bool fromVideoDataGrid = false)
        {
            if (fromVideoDataGrid)
            {
                if (Dispatcher.Invoke(() => DataGridVideosInfo.SelectedItems) != null)
                    ForPlaylist = Dispatcher.Invoke(() => DataGridVideosInfo.SelectedItems).OfType<Video>().ToList();
            }
            string daumPlaylistTemplayer = $"DAUMPLAYLIST\n" +
                    $"playname = %REPLACE%\n" +
                    $"topindex = 0\n" +
                    $"saveplaypos = 0\n";
            if (ForPlaylist == null || ForPlaylist.Count == 0)
                return;
            daumPlaylistTemplayer = daumPlaylistTemplayer.Replace("%REPLACE%", ForPlaylist[0].Url);
            int i = 1;
            foreach (Video video in ForPlaylist)
            {
                daumPlaylistTemplayer += $"{i} * file * {video.Url}\n";
                i++;
            }
            string playListFileName = Environment.CurrentDirectory + $"\\{DateTime.Now:hh.mm.ss}.dpl";
            File.WriteAllText($@"{playListFileName}", daumPlaylistTemplayer);
            Process.Start(player, playListFileName);
        }
        private void TryStartPlaylist(List<Video> forPlaylist)
        {
            string daumPlaylistTemplayer = $"DAUMPLAYLIST\n" +
                    $"playname = %REPLACE%\n" +
                    $"topindex = 0\n" +
                    $"saveplaypos = 0\n";
            if (forPlaylist == null || forPlaylist.Count == 0)
                return;
            daumPlaylistTemplayer = daumPlaylistTemplayer.Replace("%REPLACE%", forPlaylist[0].Url);
            int i = 1;
            foreach (Video video in forPlaylist)
            {
                daumPlaylistTemplayer += $"{i} * file * {video.Url}\n";
                i++;
            }
            string playListFileName = Environment.CurrentDirectory + $"\\{DateTime.Now:hh.mm.ss}.dpl";
            File.WriteAllText($@"{playListFileName}", daumPlaylistTemplayer);
            Process.Start(player, playListFileName);
        }
        private async Task GoThroughTheImages(List<Video> videos)
        {
            foreach (var x in videos)
            {
                string thumbnail = Path.Combine(@"E:\My_thumbnails", Path.GetFileName(x.Url) + ".jpg");
                Dispatcher.Invoke(() => LoadImage(thumbnail));
                await Task.Delay(300);
            }
        }
        private async void DataGridGirlsInfo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {

                if (DataGridGirlsInfo.SelectedItems is null)
                {
                    Dispatcher.Invoke(() => TotalSelectedGirls.Text = $"Вьіделено: 0");
                    return;
                }
                int count = DataGridGirlsInfo.SelectedItems.Count;
                Dispatcher.Invoke(() => TotalSelectedGirls.Text = $"Вьіделено: {count}");
                //TODO изменить логику
                Girl lastSelected = DataGridGirlsInfo.SelectedItems[0] as Girl;
                if (lastSelected is null)
                    return;
                var girlVideoInfo = GetVideosFromSQL().Where(x => x != null && x.girlId == lastSelected.Id).ToList();
                // TODO IGNORING
                //    temp.ForEach(x =>
                //{
                //    x.Size = (long.TryParse(x.Size, out long res) ? res / 1024 / 1024 : 0).ToString();
                //});
                ForPlaylist = girlVideoInfo;
                DataGridVideosInfo.ItemsSource = ForPlaylist;
                await ShowImage(lastSelected);
                //TODO предпросмотр
                //await GoThroughTheImages(temp);
            }
            catch (Exception exc)
            {
                Debug.WriteLine(exc.Message);
            }
        }
        private async Task DownloadImage(string url, string filePath)
        {
            using (HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
            using (Stream streamToWriteTo = File.Open(filePath, FileMode.Create))
                await streamToReadFrom.CopyToAsync(streamToWriteTo);

        }
        private async Task ShowImage(Girl girl)
        {
            string url = girl.Ava;
            //Debug.WriteLine(url);
            string filePath = Path.Combine(Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory), ManageDataGrids.FilesFolder, Path.GetFileName(url));
            //Debug.WriteLine(filePath);
            if (!File.Exists(filePath))
            {
                await DownloadImage(url, filePath);
            }

            BitmapImage bi3 = new BitmapImage();
            bi3.CacheOption = BitmapCacheOption.OnDemand;
            //bi3.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            bi3.BeginInit();
            bi3.UriSource = new Uri(filePath, UriKind.Absolute);
            bi3.EndInit();
            ImagePreview.Stretch = Stretch.UniformToFill;
            ImagePreview.Source = bi3;
            await Dispatcher.InvokeAsync(() => avaCurrent.Text = $"Ава:{girl.Name}");

        }
        private async void Click_ToPlayAll(object sender, RoutedEventArgs e)
        {
            await AnotherMethod();
        }
        private async void Click_OpenFolder(object sender, RoutedEventArgs e)
        {
            await OpenFolder();
        }
        private async void Click_ToPlayWithDuraion(object sender, RoutedEventArgs e)
        {
            await AnotherMethod(true);
        }
        private async Task AnotherMethod(bool withDuration = false)
        {
            await Task.Run(() =>
            {
                //TODO переделать избавиться от дубликатов
                var videos = ManySelectedVideos(withDuration).Distinct(x => x).ToList();
                TryStartPlaylist(videos);
            });
        }
        private async Task OpenFolder()
        {
            var items = DataGridGirlsInfo.SelectedItems;
            if (items == null)
                return;
            var item = items[0] as Girl;

            string girlFolder = Path.GetFileNameWithoutExtension(item.Link);
            Process.Start("explorer.exe", Path.Combine(RootDatabaseFileFolder, girlFolder));
        }
        private List<Video> ManySelectedVideos(bool withDuration)
        {
            string duration = withDuration ? Dispatcher.Invoke(() => tBox49.Text) : "0";
            var videoList = GetVideosFromSQL();
            List<Video> videos = new List<Video>();
            var state = GetGirlDownloadState();
            var selecteds = Dispatcher.Invoke(() => DataGridGirlsInfo.SelectedItems);
            foreach (var item in selecteds)
            {
                var result = item as Girl;
                if (result == null || string.IsNullOrEmpty(result.Link))
                    continue;
                ComparingSign sign = ReturnComparingSign(Dispatcher.Invoke(() => tBox49.Text));
                string girlFolder = Path.GetFileNameWithoutExtension(result.Link);
                //TODO Ignoring
                if (!withDuration)
                    videos.AddRange(videoList.Where(x => x.girlId == result.Id));
                var vids = videoList.Where(x => x.girlId == result.Id && !string.IsNullOrEmpty(x.Url))
                    .Where(y => GetDurationRange(y.Quality, duration, y.Duration));
                if (Dispatcher.Invoke(() => Offline.IsChecked == true) && state[result.Link])
                    videos.AddRange(vids.Select(x => new Video() { Url = Path.Combine(Path.Combine(RootDatabaseFileFolder, girlFolder, "videos", Path.GetFileName(x.Url))) }));
                else if (Dispatcher.Invoke(() => Offline.IsChecked == true) && !state[result.Link])
                    continue;
                else
                    videos.AddRange(vids);
            }
            return videos;
        }
        private TimeSpan MinutesToTimeSpan(string minutes)
        {
            if (int.TryParse(minutes, out int intResult))
                return new TimeSpan(0, intResult, 0);
            if (double.TryParse(minutes, out double doubleResult))
                return new TimeSpan(0, Convert.ToInt32(doubleResult), 0);
            else
                return new TimeSpan(0, 0, 0);
        }
        private TimeSpan ParseTimeSpan(string str)
        {
            if (TimeSpan.TryParse(str, out TimeSpan result))
                return result;
            return new TimeSpan(0);
        }
        private async void Click_ToMakeHtml(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                VideoGirls videoGirls = YamlDeSerialize();
                List<Video> videos = new List<Video>();
                var selecteds = Dispatcher.Invoke(() => DataGridGirlsInfo.SelectedItems);
                int toMake = selecteds.Count;
                if (toMake > 10)
                {
                    var result = MessageBox.Show($"Слишком много выбрано ({toMake})! Вы точно хотите продолжить как есть или нажмите No, чтобы обработать первые 10. Cancel для отмены", "Предупреждение", MessageBoxButton.YesNoCancel, MessageBoxImage.Exclamation); ;
                    if (result == MessageBoxResult.Cancel)
                        return;
                    if (result != MessageBoxResult.Yes)
                        toMake = 10;
                }
                for (int i = 0; i < toMake; i++)
                {
                    var result = selecteds[i] as Girl;
                    MakeHtml(result);
                }
            });
        }


        private async void CheckDeleted_Click(object sender, RoutedEventArgs e)
        {
            await CheckDeletedChicks();
        }
        /// <summary>
        /// Проверка по ссылкам
        /// </summary>
        /// <returns></returns>
        private async Task CheckDeletedChicks()
        {
            int max = IsIntValid(txtboxMax.Text);
            if (max == -1)
                return;
            List<string> GirlsInDb = new List<string>();
            if (string.IsNullOrWhiteSpace(tBox14.Text))
            {
                using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
                {
                    DB.Open();
                    var CMD = DB.CreateCommand();
                    CMD.CommandText = $@"select link from Test";
                    var SQL = CMD.ExecuteReader();
                    {
                        if (SQL.HasRows)
                        {
                            while (SQL.Read())
                            {
                                string link = string.IsNullOrWhiteSpace(NewHost) ? SQL.GetString(0).Trim() : Regex.Replace(SQL.GetString(0).Trim(), @"https?:\/\/\w+-\w+\.\w+\/", NewHost + "/");
                                GirlsInDb.Add(link);
                            }
                        }
                    }
                }
            }
            else
                GirlsInDb = tBox14.Text.Split('\n').ToList();
            List<string> outlist = new List<string>();
            Parallel.For(1, max + 1, i =>
            {
                using (WebClient wc = new WebClient())
                {
                    var hDoc = new HtmlDocument();
                    hDoc.LoadHtml(wc.DownloadString($"{Host}/page/{i}/"));
                    var html = hDoc.DocumentNode.SelectSingleNode("/html").OuterHtml;
                    outlist.Add(html);
                }
            });
            var hDoc1 = new HtmlDocument();
            hDoc1.LoadHtml(string.Join("\n", outlist));
            var nodes1 = hDoc1.DocumentNode.SelectNodes(".//*[@id=\"dle-content\"]/*[@class=\"sep \"]/a");
            var nodes2 = hDoc1.DocumentNode.SelectNodes(".//*[@id=\"dle-content\"]/*[@class=\"sep newstory\"]/a");
            var list1 = nodes1.Cast<HtmlNode>().Select(x => Regex.Replace(x.GetAttributeValue("href", "SHIT"), @"https?://", ""));
            var list2 = nodes2.Cast<HtmlNode>().Select(x => Regex.Replace(x.GetAttributeValue("href", "SHIT"), @"https?://", ""));
            List<string> GirlsOnSite = list1.Concat(list2).ToList();
            List<string> deleted = new List<string>();
            foreach (string girl in GirlsInDb)
            {
                if (GirlsOnSite.FirstOrDefault(x => x == girl) == null)
                {
                    deleted.Add("http://" + girl);
                }
            }
            string output = string.Join("\n", deleted);
            tBox15.Text = output;
            using (FileStream fs = new FileStream(ManageDataGrids.DeletedCSV, FileMode.OpenOrCreate))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                    sw.Write(output);
            }
        }
        private async Task AddNotes()
        {
            string[] links;
            if (!File.Exists(ManageDataGrids.DeletedCSV))
                return;
            using (FileStream fs = new FileStream(ManageDataGrids.DeletedCSV, FileMode.Open))
            using (StreamReader sr = new StreamReader(fs))
            {
                links = sr.ReadToEnd().Split('\n');
                var result = MessageBox.Show($"Записать {links.Length} как удаленные?", "", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;
                await Task.Run(() =>
                {
                    using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
                    {
                        DB.Open();
                        foreach (string link in links)
                        {
                            var CMD = DB.CreateCommand();
                            CMD.CommandText = $"update test set notes = 'deleted' where link like '%{Regex.Match(link, @"https?://\w+-\w+\.\w+(\/\S+)").Groups[1].Value.Trim()}%'";
                            CMD.ExecuteNonQuery();
                        }
                    }
                });
            }
        }
        private async Task CheckInDataBase()
        {
            string[] names = tBox14.Text.Split('\n');
            List<string> deleted = new List<string>();
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                foreach (string name in names)
                {
                    string temp = Regex.Replace(name, @"[\s\(\)]+", " ");
                    temp = "%" + temp.Trim().Replace(" ", "%") + "%";
                    var CMD = DB.CreateCommand();
                    CMD.CommandText = $@"select link from Test where link like '{temp.Trim().ToUpper()}'";
                    var SQL = CMD.ExecuteReader();
                    if (!SQL.HasRows)
                    {

                        deleted.Add(name);
                    }
                }
            }
            tBox15.Text = string.Join("\n", deleted);
        }

        private async void OpenInChrome_Play(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                foreach (var video in ForPlaylist)
                {
                    Process.Start(@"chrome", $"{video.Url}");
                }
            });
        }

        private void tBoxHost_TextChanged(object sender, TextChangedEventArgs e)
        {
            Dispatcher.Invoke(() => NewHost = tBoxHost.Text);
            Dispatcher.Invoke(() => Host = MakeLinkFromTextBox());
        }
        private string MakeLinkFromTextBox()
        {
            return MakeLink(tBoxHost.Text);
        }
        private string MakeLink(string link)
        {
            if (!link.StartsWith("http"))
                link = "https://" + link;
            if (link.EndsWith("/"))
                return link.TrimEnd('/');
            return link;
        }
        private string NewHost { get; set; }

        Girl updateGirl;
        private void DataGridGirlsInfo_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            var t = e.Row.Item;
            if (t is Girl)
            {
                updateGirl = t as Girl;
                updateGirl.Notify += AsyncEvent;
            }
        }
        Video updateVideo;
        private void DataGridVideosInfo_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            var t = e.Row.Item;
            if (t is Video)
            {
                updateVideo = t as Video;
                updateVideo.Notify += AsyncEvent;
            }
        }
        private Dictionary<string, bool> GetGirlDownloadState()
        {
            Dictionary<string, bool> values = new Dictionary<string, bool>();
            try
            {
                if (DB == null)
                    DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}");
                if (!Directory.Exists(Path.Combine(RootDatabaseFileFolder)))
                    Directory.CreateDirectory(Path.Combine(RootDatabaseFileFolder));
                string json;
                string filePath = Path.Combine(RootDatabaseFileFolder, @"download state.xml");
                if (!File.Exists(filePath))
                {
                    if (DB.State != System.Data.ConnectionState.Open)
                        DB.Open();
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = $"select link from test";
                        var SQL = CMD.ExecuteReader();
                        if (SQL.HasRows)
                        {
                            while (SQL.Read())
                            {
                                values.Add((string)SQL["link"], false);
                            }
                        }

                    }
                    json = JsonConvert.SerializeObject(values, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                }
                else
                {
                    json = File.ReadAllText(filePath);
                    values = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
                    if (DB.State != System.Data.ConnectionState.Open)
                        DB.Open();
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = $"select link from test";
                        var SQL = CMD.ExecuteReader();
                        if (SQL.HasRows)
                        {
                            while (SQL.Read())
                            {
                                string t = (string)SQL["link"];
                                if (!values.ContainsKey(t))
                                    values.Add(t, false);
                            }
                        }
                    }

                    json = JsonConvert.SerializeObject(values, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                }
                return values;
            }
            catch
            {
                return values;
            }
        }
        //private async void DownloadAll()
        //{
        //    string json;
        //    string filePath = Path.Combine(RootDatabaseFileFolder, @"download state.xml");
        //    var values = GetGirlDownloadState();
        //    try
        //    {
        //        DownloadAllGirls.IsEnabled = false;
        //        await Task.Run(() =>
        //        {
        //            for (int i = 0; i < values.Count; i++)
        //            {
        //                Dispatcher.Invoke(() => tBlockCurrentId.Text = $"{i + 1}/{values.Count}");
        //                if (values.ElementAt(i).Value)
        //                    continue;
        //                GirlDownloaderSync(values.ElementAt(i).Key);
        //                values[values.ElementAt(i).Key] = true;
        //                if (breaker)
        //                    break;
        //            }
        //            breaker = false;
        //            json = JsonConvert.SerializeObject(values, Formatting.Indented);
        //            File.WriteAllText(filePath, json);
        //            Dispatcher.Invoke(() => DownloadAllGirls.IsEnabled = true);
        //        });
        //    }
        //    catch (Exception e)
        //    {
        //        MessageBox.Show(e.Message);
        //    }
        //    finally
        //    {
        //        json = JsonConvert.SerializeObject(values, Formatting.Indented);
        //        File.WriteAllText(filePath, json);
        //        Dispatcher.Invoke(() => DownloadAllGirls.IsEnabled = true);

        //    }
        //}

        private static async Task<R[]> ConcurrentAsync<T, R>(int maxConcurrency, IEnumerable<T> items, Func<T, Task<R>> createTask)
        {
            var allTasks = new List<Task<R>>();
            var activeTasks = new List<Task<R>>();
            foreach (var item in items)
            {
                if (activeTasks.Count >= maxConcurrency)
                {
                    var completedTask = await Task.WhenAny(activeTasks);
                    activeTasks.Remove(completedTask);
                }
                Console.WriteLine(DateTime.Now.ToString());
                var task = createTask(item);
                allTasks.Add(task);
                activeTasks.Add(task);
            }
            return await Task.WhenAll(allTasks);
        }

        private async void DownloadAll()
        {
            await Login(true);
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                string json;
                string filePath = Path.Combine(RootDatabaseFileFolder, @"download state.xml");
                List<string> toDownload = new List<string>();
                var values = GetGirlDownloadState();
                try
                {
                    DownloadAllGirls.IsEnabled = false;
                    for (int i = 0; i < values.Count; i++)
                    {
                        Dispatcher.Invoke(() => tBlockCurrentId.Text = $"{i + 1}/{values.Count}");
                        if (values.ElementAt(i).Value)
                            continue;
                        toDownload.Add(values.ElementAt(i).Key);
                    }
                    await ConcurrentAsync<string, bool>(2, toDownload, GirlDownloaderAsync);        //TODO ИЗМЕННЯТЬ СОСТОЯНИЕ ЗАКАЧКИ
                    json = JsonConvert.SerializeObject(values, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                    Dispatcher.Invoke(() => DownloadAllGirls.IsEnabled = true);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
                finally
                {
                    json = JsonConvert.SerializeObject(values, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                    Dispatcher.Invoke(() => DownloadAllGirls.IsEnabled = true);

                }
            }
        }
        private Task<bool> GirlDownloaderAsync(string url)
        {
            return Task.Run(() =>
            {
                return GirlDownloaderSync(url);
            });
        }
        /*
        private void Click_DownloadGirl(object sender, RoutedEventArgs e)
        {
            if (DataGridGirlsInfo.SelectedItem == null)
                return;
            var girl = DataGridGirlsInfo.SelectedItem as Girl;
            string link = girl.Link;
            var values = GetGirlDownloadState();
            if (values[link])
                MessageBox.Show("Уже скачан");
            else
            {
                Task.Run(() =>
                {
                    GirlDownloaderSync(link);
                    values[link] = true;
                    string json = JsonConvert.SerializeObject(values,Formatting.Indented);
                    string filePath = Path.Combine(RootDatabaseFileFolder, @"download state.xml");
                    File.WriteAllText(filePath, json);
                });

            }
        }*/
        private void Click_DownloadGirl(object sender, RoutedEventArgs e)
        {
            if (DataGridGirlsInfo.SelectedItem == null)
                return;
            var girl = DataGridGirlsInfo.SelectedItem as Girl;
            string link = girl.Link;
            var values = GetGirlDownloadState();
            Task.Run(() =>
            {
                GirlDownloaderSync(link);
                values[link] = true;
                string json = JsonConvert.SerializeObject(values, Formatting.Indented);
                string filePath = Path.Combine(RootDatabaseFileFolder, @"download state.xml");
                File.WriteAllText(filePath, json);
            });


        }
        private async void Click_UpdateGirl(object sender, RoutedEventArgs e)
        {
            await UpdateGirlVideoInfo();
        }

        private async Task MakeThumbnailsCmd(string url, string title)
        {
            await Task.Run(() =>
            {
                var process = new Process
                {
                    StartInfo =
                {
                    FileName = "cmd",
                    Arguments = "/C python \"G:\\Мій диск\\DOCS\\Visual Studio Code\\old_files\\thumbnails_maker.py\" " +
        "-l " + url,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
                };
                process.Start();
                string thumbnailPath = Path.Combine("E:\\My_thumbnails", Path.GetFileName(url) + ".jpg");
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (true)
                {
                    if (sw.ElapsedMilliseconds > 40000)
                        break;
                    if (File.Exists(thumbnailPath))
                    {
                        Dispatcher.Invoke(() => LoadImage(thumbnailPath, title));
                        break;
                    }
                    Task.Delay(300);
                }
            });
        }
        ImgPreview imgPreview;
        private async void DataGridVideosInfo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var items = DataGridVideosInfo.SelectedItems;
            if (items == null || items.Count < 1)
                return;
            if (items[items.Count - 1].ToString() == "{NewItemPlaceholder}")
                return;
            var item = items[items.Count - 1] as Video;
            //string name_wo_ext = Path.GetFileNameWithoutExtension(item.Title);
            string thumbnail = Path.Combine(@"E:\My_thumbnails", Path.GetFileName(item.Url) + ".jpg");
            Dispatcher.Invoke(() => LoadImage(thumbnail));
        }
        private void LoadImage(string thumbnail, string title = "ImgPreview")
        {
            Debug.WriteLine(title);
            if (File.Exists(thumbnail))
            {
                if (Dispatcher.Invoke(() => allowPreview.IsChecked) == false)
                    return;
                if (imgPreview == null)
                {
                    imgPreview = new ImgPreview();
                    imgPreview.WindowStartupLocation = WindowStartupLocation.Manual;
                    imgPreview.Left = 1920 - 800;
                    imgPreview.Top = 0;
                    imgPreview.Show();
                    imgPreview.Closed += ImgPreview_Closed;
                    imgPreview.Deactivated += ImgPreview_Deactivated;
                    //mainWindow.Activate();
                }
                imgPreview.Title = title;
                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (true)
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(thumbnail);
                        imgPreview.image1.Source = ToImage(bytes);
                        break;
                    }
                    catch(Exception err)
                    {
                        if (sw.ElapsedMilliseconds > 1000)
                            break;
                        //Debug.WriteLine(sw.ElapsedMilliseconds);
                    }

                }

                //imgPreview.image1.Source = new BitmapImage(new Uri("file:///" + thumbnail.Replace("#", @"%23")));
            }
            else
            {
                if (Dispatcher.Invoke(() => allowPreview.IsChecked) == false)
                    return;
                if (imgPreview == null)
                {
                    imgPreview = new ImgPreview();
                    imgPreview.WindowStartupLocation = WindowStartupLocation.Manual;
                    imgPreview.Left = 1920 - 800;
                    imgPreview.Top = 0;
                    imgPreview.Show();
                    imgPreview.Closed += ImgPreview_Closed;
                    imgPreview.Deactivated += ImgPreview_Deactivated;
                    //mainWindow.Activate();
                }
                imgPreview.image1.Source = CreateBitmapImage();
            }
        }
        public BitmapImage ToImage(byte[] array)
        {
            using (var ms = new System.IO.MemoryStream(array))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // here
                image.StreamSource = ms;
                image.EndInit();
                return image;
            }
        }
        private BitmapSource CreateBitmapImage()
        {
            int width = 800;
            int height = width;
            int stride = width / 8;
            byte[] pixels = new byte[height * stride];

            // Try creating a new image with a custom palette.
            List<System.Windows.Media.Color> colors = new List<System.Windows.Media.Color>();
            colors.Add(System.Windows.Media.Colors.Black);
            BitmapPalette myPalette = new BitmapPalette(colors);

            // Creates a new empty image with the pre-defined palette
            BitmapSource image = BitmapSource.Create(
                                                     width, height,
                                                     96, 96,
                                                     PixelFormats.Indexed1,
                                                     myPalette,
                                                     pixels,
                                                     stride);
            return image;
        }
        private void ImgPreview_Closed(object sender, EventArgs e)
        {
            imgPreview = null;
        }
        private void ImgPreview_Deactivated(object sender, EventArgs e)
        {

            Window window = (Window)sender;
            if (mainWindow.IsActive)
                window.Topmost = true;
            else
                window.Topmost = false;
        }
        private List<Video> GetVideosFromSQL()
        {
            List<Video> list = new List<Video>();
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = "select * from videos";
                    using (SQLiteDataReader SQL = CMD.ExecuteReader())
                    {
                        if (SQL.HasRows)
                        {
                            while (SQL.Read())
                            {
                                Video video = new Video();
                                video.Url = SQL["url"] == DBNull.Value ? null : SQL.GetString(1);
                                video.Size = SQL["size"] == DBNull.Value ? null : SQL.GetString(3);
                                video.Quality = SQL["quality"] == DBNull.Value ? null : SQL.GetString(4);
                                video.Duration = SQL["duration"] == DBNull.Value ? null : SQL.GetString(5);
                                video.Notes = SQL["Notes"] == DBNull.Value ? null : SQL.GetString(6);
                                video.girlId = SQL.GetInt64(7);
                                list.Add(video);
                            }
                        }
                    }
                }
            }
            return list;
        }
        private void UpdateVideoInfo(Video video)
        {
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = $"update videos " +
                        $"set " +
                        $"url = '{video.Url}', " +
                        $"size = '{video.Size}', " +
                        $"quality = '{video.Quality}', " +
                        $"duration = '{video.Duration}' " +
                        $"where filename == '{video.Filename}'";
                    Debug.WriteLine(CMD.CommandText);
                    CMD.ExecuteNonQuery();
                }
            }
        }


        private async Task UpdateGirlVideoInfo()
        {
            if (DataGridGirlsInfo.SelectedItem == null)
                return;
            var temp = DataGridGirlsInfo.SelectedItems;
            List<Girl> items;
            temp.Remove(null);
            items = temp.OfType<Girl>().ToList();
            int count = items.Count;
            var result = MessageBox.Show("Update only where there is no thumbnail?", "What to do?", MessageBoxButton.YesNo, MessageBoxImage.Question);
            await Login(true);
            List<Video> videos = GetVideosFromSQL();
            for (int i = 0; i < count; i++)
            {
                var girl = items[i] as Girl;
                string link = girl.Link;
                if (result == MessageBoxResult.Yes)
                {
                    bool thumbnailStatus = false;
                    foreach (string vid in girl.Videos)
                    {
                        string thumbnailPath = Path.Combine("E:\\My_thumbnails", Path.GetFileName(vid) + ".jpg");
                        if (!File.Exists(thumbnailPath))
                            thumbnailStatus = true;
                    }
                    if (!thumbnailStatus)
                        continue;
                }
                var updatedGirl = await To_list(girl.Link);
                if (updatedGirl == null)
                    return;

                if (videos.Any(x => x.girlId == girl.Id))
                {
                    foreach (var newVideo in updatedGirl.Videos)
                    {
                        bool flag = false;
                        foreach (var video in videos.Where(x => x.girlId == girl.Id))
                        {
                            var mx = Regex.Match(newVideo, @"\/video\d?\/(.+?.mp4)").Groups[1].Value;
                            var murl = Regex.Match(video.Url, @"\/video\d?\/(.+?.mp4)").Groups[1].Value;
                            if (mx == murl)
                            {
                                Video updatedVideo;
                                if (string.IsNullOrEmpty(video.Duration))
                                {
                                    updatedVideo = await UpdateVideoInfo(newVideo);
                                    updatedVideo.girlId = girl.Id;
                                }
                                else
                                    updatedVideo = new Video() { Url = newVideo, Duration = video.Duration, Quality = video.Quality, Size = video.Size, girlId = video.girlId };
                                //
                                string thumbnailPath = Path.Combine("E:\\My_thumbnails", Path.GetFileName(updatedVideo.Url) + ".jpg");
                                if (!File.Exists(thumbnailPath))
                                {
                                    Debug.WriteLine(updatedVideo.Url);
                                    await MakeThumbnailsCmd(updatedVideo.Url, updatedGirl.Name);
                                }
                                UpdateVideoInfo(updatedVideo);
                                flag = true;
                                break;
                            }

                        }
                        if (!flag)
                        {
                            WriteVideoToSQL(girl.Id, new Video() { Url = newVideo, girlId = girl.Id });
                        }
                    }
                }
                else
                {
                    //TODO лишнее мьі добавляем видео раньше
                }
                using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
                {
                    DB.Open();
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = $"UPDATE test SET videos='{string.Join("\n", updatedGirl.Videos)}' WHERE Link='{girl.Link}'";
                        CMD.ExecuteNonQuery();
                    }
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = $"UPDATE test SET lastUpdate='{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}' WHERE linkid='{girl.LinkId}'";
                        CMD.ExecuteNonQuery();
                    }
                }
                Dispatcher.Invoke(() => status.Text = $" {i + 1}/{count} " + girl.Name + " " + DateTime.Now.ToString("HH:mm:ss"));
            }
        }

        //private async Task UpdateGirlVideoInfo()
        //{
        //    if (DataGridGirlsInfo.SelectedItem == null)
        //        return;
        //    var items = DataGridGirlsInfo.SelectedItems.Cast<Girl>().ToList();
        //    int count = items.Count;
        //    var result = MessageBox.Show("Update only where there is no thumbnail?", "What to do?", MessageBoxButton.YesNo, MessageBoxImage.Question);
        //    await Login(true);
        //    for (int i = 0; i < count; i++)
        //    {
        //        var girl = items[i] as Girl;
        //        string link = girl.Link;
        //        if (result == MessageBoxResult.Yes)
        //        {
        //            bool thumbnailStatus = false;
        //            foreach (string vid in girl.Videos)
        //            {
        //                string thumbnailPath = Path.Combine("E:\\My_thumbnails", Path.GetFileName(vid) + ".jpg");
        //                if (!File.Exists(thumbnailPath))
        //                    thumbnailStatus = true;
        //            }
        //            if (!thumbnailStatus)
        //                continue;
        //        }
        //        var updatedGirl = await To_list(girl.Link);
        //        if (updatedGirl == null)
        //            return;
        //        VideoGirls videoGirls = YamlDeSerialize();
        //        List<Video> videos = new List<Video>();
        //        VideoProperties properties = new VideoProperties();
        //        if (videoGirls.Properties.Any(x => x.GirlPage == link))
        //        {
        //            properties = videoGirls.Properties.Where(x => x.GirlPage == link).First();
        //            var item = videoGirls.Properties.FirstOrDefault(x => x.GirlPage == link);
        //            videoGirls.Properties.Remove(item);
        //            foreach (var newVideo in updatedGirl.Videos)
        //            {
        //                bool flag = false;
        //                foreach (var video in properties.Videos)
        //                {
        //                    var mx = Regex.Match(newVideo, @"\/video\d?\/(.+?.mp4)").Groups[1].Value;
        //                    var murl = Regex.Match(video.Url, @"\/video\d?\/(.+?.mp4)").Groups[1].Value;
        //                    if (mx == murl)
        //                    {
        //                        Debug.WriteLine("new " + newVideo + " old " + video.Url);
        //                        var deleteThis = properties.Videos.Where(y => y.Url == video.Url).FirstOrDefault();
        //                        Video updatedVideo;
        //                        if (string.IsNullOrEmpty(video.Duration))
        //                        {
        //                            updatedVideo = await UpdateVideoInfo(newVideo);
        //                        }
        //                        else
        //                            updatedVideo = new Video() { Url = newVideo, Duration = video.Duration, Quality = video.Quality, Size = video.Size };
        //                        //
        //                        string thumbnailPath = Path.Combine("E:\\My_thumbnails", Path.GetFileName(updatedVideo.Url) + ".jpg");
        //                        if (!File.Exists(thumbnailPath))
        //                        {
        //                            Debug.WriteLine(updatedVideo.Url);
        //                            await MakeThumbnailsCmd(updatedVideo.Url, updatedGirl.Name);
        //                        }
        //                        //
        //                        properties.Videos.Remove(deleteThis);
        //                        properties.Videos.Add(updatedVideo);
        //                        flag = true;
        //                        break;
        //                    }

        //                }
        //                if (!flag)
        //                {
        //                    properties.Videos.Add(new Video() { Url = newVideo });
        //                }
        //            }
        //        }
        //        else
        //        {
        //            properties.GirlPage = link;
        //            updatedGirl.Videos.ForEach(x =>
        //            {
        //                properties.Videos.Add(new Video() { Url = x });
        //            });
        //        }
        //        videoGirls.Properties.Add(properties);
        //        YamlSerialize(videoGirls);
        //        using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
        //        {
        //            DB.Open();
        //            using (SQLiteCommand CMD = DB.CreateCommand())
        //            {
        //                CMD.CommandText = $"UPDATE test SET videos='{string.Join("\n", updatedGirl.Videos)}' WHERE Link='{girl.Link}'";
        //                CMD.ExecuteNonQuery();
        //            }
        //            using (SQLiteCommand CMD = DB.CreateCommand())
        //            {
        //                CMD.CommandText = $"UPDATE test SET lastUpdate='{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}' WHERE linkid='{girl.LinkId}'";
        //                CMD.ExecuteNonQuery();
        //            }
        //        }
        //        Dispatcher.Invoke(() => status.Text = $" {i + 1}/{count} " + girl.Name + " " + DateTime.Now.ToString("HH:mm:ss"));
        //    }
        //}
        private async Task ListDownload(IEnumerable<string> imageLinkArray, string folderPath)
        {
            await Task.Run(() =>
            {
                string fileName;
                foreach (string link in imageLinkArray)
                {
                    fileName = Path.Combine(folderPath, Path.GetFileName(link));
                    DownloadFileSync(link, fileName);
                }
            });
        }
        private void ListDownloadSync(IEnumerable<string> imageLinkArray, string folderPath)
        {
            string fileName;
            foreach (string link in imageLinkArray)
            {
                fileName = Path.Combine(folderPath, Path.GetFileName(link));
                DownloadFileSync(link, fileName);
            }
        }
        private bool GirlDownloaderSync(string girlLink)
        {
            string command = $"select * from test where link == '{girlLink}'";
            Dispatcher.Invoke(() => tBlockId.Text = Regex.Match(girlLink, @"\/(\d+)\-").Groups[1].Value);
            var girls = ManageDataGrids.ReturnGirlsInfo(command);
            if (girls == null || girls.ListOfGirls == null || girls.ListOfGirls.Count < 1)
                return false;
            var girl = girls.ListOfGirls[0];
            var newInfo = To_list(girl.Link).Result;
            if (newInfo is null)
                return false;
            girl.Videos = newInfo.Videos;
            string girlFolder = Path.GetFileNameWithoutExtension(girl.Link);
            Directory.CreateDirectory(Path.Combine(RootDatabaseFileFolder, girlFolder));
            DownloadFileSync(girl.Ava, Path.Combine(RootDatabaseFileFolder, girlFolder, Path.GetFileName(girl.Ava)));//сделать асинхронно?
            Directory.CreateDirectory(Path.Combine(RootDatabaseFileFolder, girlFolder, "images"));
            ListDownloadSync(girl.Images, Path.Combine(RootDatabaseFileFolder, girlFolder, "images"));
            Directory.CreateDirectory(Path.Combine(RootDatabaseFileFolder, girlFolder, "videos"));
            ListDownloadSync(girl.Videos, Path.Combine(RootDatabaseFileFolder, girlFolder, "videos"));
            return true;
        }
        private async void AsyncEvent(Girl girl)
        {
            await Task.Run(() =>
            {
                using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath};Journal Mode=Off"))
                {
                    DB.Open();
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = $"UPDATE test SET Notes='{girl.Notes?.ToUpper()}' WHERE Link='{girl.Link}'";
                        CMD.ExecuteNonQuery();
                    }
                }
            });
        }
        private async void AsyncEvent(Video video)
        {
            await Task.Run(() =>
            {
                using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath};Journal Mode=Off"))
                {
                    DB.Open();
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = $"UPDATE videos SET Notes='{video.Notes?.ToUpper()}' WHERE Url='{video.Url}'";
                        CMD.ExecuteNonQuery();
                    }
                }
            });
        }
        private void UpdateGirl(Girl girl, bool onlyUpdatedField = false)
        {
            try
            {
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    if (!onlyUpdatedField)
                        CMD.CommandText = $"UPDATE test SET lastUpdate='{DateTime.Now + TimeSpan.FromDays(14):yyyy-MM-dd}' WHERE linkid='{girl.LinkId}'";
                    else
                        CMD.CommandText = $"UPDATE test SET notes='update', lastUpdate='{DateTime.MaxValue.ToString("yyyy-MM-dd")}' WHERE linkid='{girl.LinkId}'";
                    CMD.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }

        }
        private void AddUpdatedGirl(Girl girl)
        {
            using (SQLiteCommand CMD = DB.CreateCommand())
            {
                CMD.CommandText = "insert into Test(name, birthdate,city,adddate,socnet,images,agethen,videos,dateofstate,linkid,link,birthdatestr,views,rating,ava,lastUpdate,notes) " +
                          "values( @name ,@birthdate, @city, @adddate,@socnet, @images,@agethen, @videos, @dateofstate, @linkid, @link,@birthdatestr,@views,@rating,@ava,@lastUpdate,@notes)";
                CMD.Parameters.Add("@name", System.Data.DbType.String).Value = girl.Name;
                CMD.Parameters.Add("@birthdate", System.Data.DbType.String).Value = girl.BirthDate.ToString("yyyy-MM-dd");
                CMD.Parameters.Add("@city", System.Data.DbType.String).Value = girl.City;
                CMD.Parameters.Add("@agethen", System.Data.DbType.Decimal).Value = girl.AgeThen;
                CMD.Parameters.Add("@adddate", System.Data.DbType.String).Value = girl.AddDate.ToString("yyyy-MM-dd");
                CMD.Parameters.Add("@socnet", System.Data.DbType.String).Value = girl.Socials;
                CMD.Parameters.Add("@images", System.Data.DbType.String).Value = string.Join("\n", girl.Images);
                CMD.Parameters.Add("@videos", System.Data.DbType.String).Value = string.Join("\n", girl.Videos);
                CMD.Parameters.Add("@dateofstate", System.Data.DbType.String).Value = girl.DateOfState.ToString("yyyy-MM-dd");
                CMD.Parameters.Add("@linkid", System.Data.DbType.Int32).Value = girl.LinkId;
                CMD.Parameters.Add("@link", System.Data.DbType.String).Value = girl.Link;
                CMD.Parameters.Add("@birthdatestr", System.Data.DbType.String).Value = girl.BirthDateAsIs;
                CMD.Parameters.Add("@views", System.Data.DbType.Int32).Value = girl.Views;
                CMD.Parameters.Add("@rating", System.Data.DbType.Int32).Value = girl.Rating;
                CMD.Parameters.Add("@ava", System.Data.DbType.String).Value = girl.Ava;
                CMD.Parameters.Add("@notes", System.Data.DbType.String).Value = "update";
                CMD.Parameters.Add("@lastUpdate", System.Data.DbType.String).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                CMD.ExecuteNonQuery();
            }

        }
        private Girl QueryToGirl(SQLiteDataReader SQL)
        {
            Girl girl = new Girl();
            while (SQL.Read())
            {
                girl.Name = (string)SQL["name"];
                girl.City = (string)SQL["city"];
                girl.AddDate = Convert.ToDateTime((string)SQL["adddate"]);
                girl.DateOfState = Convert.ToDateTime((string)SQL["dateofstate"]);
                girl.Images = ((string)SQL["images"]).Split('\n').ToList();
                string vids = (string)SQL["videos"];
                girl.Videos = vids == "" ? new List<string>() : vids.Split('\n').ToList();
                girl.BirthDate = Convert.ToDateTime((string)SQL["birthdate"]);
                girl.AgeThen = (decimal)SQL["agethen"];
                girl.Socials = (string)SQL["socnet"];
                girl.Link = (string)SQL["link"];
                girl.BirthDateAsIs = (string)SQL["birthdatestr"];
                girl.Rating = Convert.ToInt32((decimal)SQL["rating"]);
                girl.Views = Convert.ToInt32((decimal)SQL["views"]);
                girl.Notes = SQL["notes"] == DBNull.Value ? null : (string)SQL["notes"];
                girl.LinkId = Convert.ToInt32((decimal)SQL["linkid"]);
            }
            return girl;
        }
        private async Task<Girl> UpdatedGirlInfo(string link)
        {
            Girl girl = new Girl();
            try
            {
                HtmlDocument htmldoc = await Page_parsing(link, true);//TODO asynchronously
                var add_date_node = htmldoc.DocumentNode.SelectSingleNode("//*[@id=\"dle-content\"]/span/a[1]");
                if (add_date_node == null)
                    return girl;
                var add_date = add_date_node.InnerText;
                var fullinfo_nodes = htmldoc.DocumentNode.SelectNodes("//*[@id=\"dle-content\"]/div[5]/div[2]/ul/li");
                var fullinfo_li = fullinfo_nodes.Cast<HtmlNode>().Select(x => x.InnerText.Trim()).ToList();
                var fullpost_nodes = htmldoc.DocumentNode.SelectNodes("//*[@id=\"dle-content\"]/div[5]/div[3]/a");
                var fullpost_hrefs = fullpost_nodes.Cast<HtmlNode>().Select(x => x.GetAttributeValue("href", "SHIT")).Select(x => Host + x).ToList();
                var ava = Host + htmldoc.DocumentNode.SelectSingleNode("//*[@id=\"dle-content\"]/div[5]/div[1]/img").GetAttributeValue("src", "SHIT");
                int views = Convert.ToInt32(Regex.Match(htmldoc.DocumentNode.SelectSingleNode("//*[@id=\"dle-content\"]/span").InnerText,
                    @"Просмотров:(\s+(\d{1,8}))").Groups[2].Value);
                HtmlNode rating_node = htmldoc.DocumentNode.SelectSingleNode("/html/body/div[5]/div/div[1]/div/div[3]/div/ul/li[1]");
                int rating = rating_node == null ? 0 : Rating_Int(rating_node.InnerText);
                List<string> srcs = new List<string>();
                var vvideo_nodes = htmldoc.DocumentNode.SelectNodes("//*[@id=\"dle-content\"]/div[5]/div[4]/div/div/video/source");
                if (vvideo_nodes != null && vvideo_nodes.Count != 0)
                {
                    for (int i = 1; i <= vvideo_nodes.Count(); i++)
                    {
                        try
                        {
                            srcs.Add(Host + htmldoc.DocumentNode.SelectSingleNode($"//*[@id=\"dle-content\"]/div[5]/div[4]/div[{i}]/div/video/source").GetAttributeValue("src", "SHIT"));
                        }
                        catch (Exception exc)
                        {

                        }
                    }
                }
                girl.Name = fullinfo_li[0].ToUpper();
                girl.BirthDateAsIs = fullinfo_li[1].Replace("Возраст:", "").Trim();
                girl.BirthDate = Date_Parser(girl.BirthDateAsIs);
                girl.City = fullinfo_li[2].Replace("Город:", "").Trim().ToUpper();
                girl.Socials = fullinfo_li.Count() > 3 ? string.Join("\n",
                fullinfo_li.GetRange(3, fullinfo_li.Count() - 3)) : null;
                girl.DateOfState = DateTime.Now;
                girl.AddDate = Date_Parser(add_date);
                if (girl.BirthDateAsIs.Length == 2)
                    girl.AgeThen = Get_Age(girl.BirthDateAsIs);
                else
                    girl.AgeThen = Get_Age(girl.AddDate, girl.BirthDate);
                girl.Images = fullpost_hrefs;
                girl.Videos = srcs;
                girl.LinkId = Convert.ToInt32(Regex.Match(link, @"\/(\d{1,})-").Groups[1].Value);
                girl.Link = link;
                girl.Ava = ava;
                girl.Views = views;
                girl.Rating = rating;

                return girl;
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                return girl;
            }
        }
        private async void UpdateDatabase_Click(object sender, RoutedEventArgs e)
        {
            breaker = false;
            await Login();
            //await Task.Run(() => UpdateDataBase());
            await Task.Run(() => CompareGirlInfo());
        }

        private async Task CompareGirlInfo()
        {
            using (DB = new SQLiteConnection($"Data Source={ManageDataGrids.DBPath}"))
            {
                DB.Open();
                List<string> toUpdate = new List<string>();
                Girl oldGirl, newGirl = null;
                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = $"select link, lastAccess, lastUpdate from Test";
                    using (var SQL = CMD.ExecuteReader())
                    {
                        if (SQL.HasRows)
                        {
                            while (SQL.Read())
                            {
                                string lastAccess = SQL["lastAccess"] is System.DBNull ? null : (string)SQL["lastAccess"];
                                DateTime d;
                                if ((d = Date_Parser(lastAccess)) == DateTime.MinValue || DateTime.Now > (d + TimeSpan.FromDays(5)))
                                    toUpdate.Add((string)SQL["link"]);

                            }
                        }
                    }
                }
                foreach (var link in toUpdate)
                {
                    if (breaker)
                        break;
                    using (SQLiteCommand CMD = DB.CreateCommand())
                    {
                        CMD.CommandText = $"select * from Test where link == '{link}'";
                        oldGirl = QueryToGirl(CMD.ExecuteReader());
                        newGirl = await UpdatedGirlInfo(link);
                        if (newGirl.Link == null)
                            continue;
                        var newVids = CheckVideos(oldGirl, newGirl);
                        var newImgs = CheckPhotos(oldGirl, newGirl);
                        if (newVids.Count > 0 || newImgs.Count > 0)
                        {
                            Debug.WriteLine(newGirl.Link);
                            if (newVids.Count > 0)
                            {
                                newGirl.Videos = newVids;
                            }
                            if (newImgs.Count > 0)
                            {
                                newGirl.Images = newImgs;
                            }
                            WriteToOpenDB(newGirl, true);
                            var values = GetGirlDownloadState();
                            //GirlDownloaderSync(link);
                            values[link] = false;
                            string json = JsonConvert.SerializeObject(values, Formatting.Indented);
                            string filePath = Path.Combine(RootDatabaseFileFolder, @"download state.xml");
                            File.WriteAllText(filePath, json);
                        }
                        else
                        {
                            WriteToOpenDB(newGirl);
                        }
                        //TODO something



                    }

                }

            }
        }

        private void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            DownloadAll();
        }
        private List<string> CheckVideos(Girl oldGirl, Girl newGirl)
        {
            List<string> newVids = newGirl.Videos.Select(x => Path.GetFileName(x)).ToList();
            //bool ok = newVids.SequenceEqual(oldGirl.Videos.Select(x => Path.GetFileName(x)));
            //if (!ok)
            //    Debug.WriteLine("No such videos");
            string girlFolder = Path.GetFileNameWithoutExtension(newGirl.Link);
            var vidsExist = newVids.Select(x => File.Exists(Path.Combine(RootDatabaseFileFolder, girlFolder, "videos", x))).ToList();
            var vidsDownloadedNoLink = new List<string>();
            if (vidsExist.All(x => x == true))
                return new List<string>();
            for (int i = 0; i < vidsExist.Count; i++)
            {
                if (vidsExist[i])
                {
                    string t = oldGirl.Videos.Where(x => x.Contains(newVids[i])).FirstOrDefault();
                    if (t != null)
                        oldGirl.Videos.Remove(t);//TODO А ЕСЛИ ВИДЕО ЗАГРУЖЕНО, НО ССЬІЛКИ НА НЕГО БОЛЬШЕ НЕТ
                    continue;
                }
                string el = oldGirl.Videos.Where(x => x.Contains(newVids[i])).FirstOrDefault();
                if (el != null)
                    oldGirl.Videos.Remove(el);
            }
            return oldGirl.Videos.Union(newGirl.Videos).ToList();
        }

        private List<string> CheckPhotos(Girl oldGirl, Girl newGirl)
        {
            List<string> newImgs = newGirl.Images.Select(x => Path.GetFileName(x)).ToList();
            //bool ok = newImg.SequenceEqual(oldGirl.Images.Select(x => Path.GetFileName(x)));
            //if (!ok)
            //    Debug.WriteLine("No such images");
            string girlFolder = Path.GetFileNameWithoutExtension(newGirl.Link);
            var imgsExist = newImgs.Select(x => File.Exists(Path.Combine(RootDatabaseFileFolder, girlFolder, "images", x))).ToList();
            if (imgsExist.All(x => x == true))
                return new List<string>();
            for (int i = 0; i < imgsExist.Count; i++)
            {
                if (imgsExist[i])
                {
                    string t = oldGirl.Images.Where(x => x.Contains(newImgs[i])).FirstOrDefault();
                    if (t != null)
                        oldGirl.Images.Remove(t);
                    continue;
                }
                var el = oldGirl.Images.Where(x => x.Contains(newImgs[i])).FirstOrDefault();
                if (el != null)
                    oldGirl.Images.Remove(el);
            }
            return oldGirl.Images.Union(newGirl.Images).ToList();
        }
    }


}