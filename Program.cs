using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Drawing.Imaging;
using ShellProgressBar;

namespace PhotoOrganizer
{
    class Program
    {

        static string application_start_time = $"{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day} {DateTime.Now.Hour}-{DateTime.Now.Minute}";

        static ProgressBarOptions ProgressOptions = new ProgressBarOptions
        {
            ProgressBarOnBottom = true,
            ForegroundColor = Console.ForegroundColor, // Green is tacky AF.
            BackgroundCharacter = '\u2593'
        };

        static void Main(string[] args)
        {

            // TODO detect if file is onedrive, on demand specefically the not synced one.

            Dictionary<int, string> Months = new Dictionary<int, string>() {
                {1,"1 - Enero" } ,
                {2,"2 - Febrero" },
                {3,"3 - Marzo" },
                {4,"4 - Abril" },
                {5,"5 - Mayo" },
                {6,"6 - Junio" },
                {7,"7 - Julio" },
                {8,"8 - Agosto" },
                {9,"9 - Septiembre" },
                {10,"10 - Octubre" },
                {11,"11 - Noviembre" },
                {12,"12 - Diciembre" }
            }; // Could expand by using CultureInfo to generate this data.

            DirectoryInfo media_dir = new DirectoryInfo(@"A:\OneDrive\Imágenes\Álbum de cámara\2010");
            FileInfo[] media = media_dir.GetFiles("*.*", SearchOption.AllDirectories);

            string output_dir = @"A:\ImageOrganizationOutputTest\";

            // You could take the pedantic approach of reading metadata, and trying to get the date taken instad of last write, but tbh I dont edit past photos, 
            // and if I do I make a copy first, if that's your case (mismatching date taken metadata and last write) then go nuts with that.

            List<Media> DatedMedia = new List<Media>();

            int lowest_year = 9999;
            int highest_year = 0;

            // Index media files and date them.
            using (var pbar = new ProgressBar(media.Length, "Indexing Files...", ProgressOptions))
            {
                foreach (var item in media)
                {
                    Log($"Indexing {item.FullName}");
                    pbar.Tick($"Indexing {item.FullName}");

                    DateTime dating_of_media = new DateTime();

                    try
                    {
                        dating_of_media = StackOverflow.GetDateTakenFromImage(item.FullName);
                    }
                    catch (ArgumentException) // also handles the exception that happens when creating an image object but its provided a video file path
                    {
                        dating_of_media = item.LastWriteTime;
                    }


                    DatedMedia.Add(new Media(dating_of_media, item));

                    // Update lowest and highest year found to aid in folder tree generation.
                    if (dating_of_media.Year < lowest_year) lowest_year = dating_of_media.Year;
                    if (dating_of_media.Year > highest_year) highest_year = dating_of_media.Year;



                }
            }
                

            // Directory tree generation.
            using (var pbar = new ProgressBar((highest_year-lowest_year)*12, "Moving Files...", ProgressOptions))
            {
                DirectoryInfo output = Directory.CreateDirectory(output_dir);
                if (output.GetFiles().Length > 0)
                {
                    Console.WriteLine("ERROR: TARGET OUTPUT DIRECTORY MUST BE EMPTY");
                }
                else
                {
                    for (int i = lowest_year; i <= highest_year; i++)
                    {
                        Directory.CreateDirectory(output_dir + i); // Create year folders
                        foreach (KeyValuePair<int, string> mkvp in Months)
                        {
                            string dir_path = output_dir + i + @"\" + mkvp.Value; // TODO use path.combine bruh
                            Log($"Creating directory {dir_path}");
                            pbar.Tick($"Creating directory {dir_path} ...");
                            Directory.CreateDirectory(dir_path); // Create month's subdirectories.
                        }
                    }
                }

            }
            

            // Moving media files to new directory
            Media[] DatedMedia_optimized = DatedMedia.ToArray();
            
            using (var pbar = new ProgressBar(DatedMedia_optimized.Length, "Moving Files...", ProgressOptions))
            {
                for (int i = 0; i < DatedMedia_optimized.Length; i++)
                {
                    string final_path = Path.Combine(
                        output_dir,
                        DatedMedia_optimized[i].Dating.Year.ToString(),
                        Months[DatedMedia_optimized[i].Dating.Month],
                        DatedMedia_optimized[i].File.Name
                        );

                    Log($"Moving {DatedMedia_optimized[i].File.FullName} to {final_path}");
                    pbar.Tick($"Moving {DatedMedia_optimized[i].File.Name} ...");
                    //DatedMedia_optimized[i].File.CopyTo(final_path);
                }
            }


           
           



            Console.WriteLine("Press any key to exit!");
            Console.ReadKey();
        }


        struct Media
        {
            public DateTime Dating;
            public FileInfo File;

            public Media(DateTime Dating, FileInfo File)
            {
                this.Dating = Dating;
                this.File = File;
            }
        }

        private static void Log(object text)
        {
            File.AppendAllText($"{application_start_time}_PhotoOrganizerLog.txt", $"\n[{DateTime.Now}]\t" + text);
        }
    }
}
