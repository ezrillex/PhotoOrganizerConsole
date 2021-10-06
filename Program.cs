using System;
using System.IO;
using System.Collections.Generic;
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

            DirectoryInfo media_dir = new DirectoryInfo(@"A:\OneDrive\Imágenes\Desorden\");
            string output_dir = @"A:\OneDrive\Imágenes\output\";


           

            FileInfo[] media = media_dir.GetFiles("*.*", SearchOption.AllDirectories);


            // You could take the pedantic approach of reading metadata, and trying to get the date taken instad of last write, but tbh I dont edit past photos, 
            // and if I do I make a copy first, if that's your case (mismatching date taken metadata and last write) then go nuts with that.

            Media[] DatedMedia = new Media[media.Length];

            int lowest_year = 9999;
            int highest_year = 0;

            // Index media files and date them.
            using (var pbar = new ProgressBar(media.Length, "Indexing Files...", ProgressOptions))
            {
                DateTime dating_of_media = new DateTime(); // reuse this to reduce stress of GC
                for (int i = 0; i < media.Length; i++)
                {
                    Log($"Indexing {media[i].FullName}");
                    pbar.Tick($"Indexing {media[i].FullName}");


                    try
                    {
                        dating_of_media = StackOverflow.GetDateTakenFromImage(media[i].FullName);
                    }
                    catch (Exception ex)
                    {
                        // Argument also handles the exception that happens when creating an image object but its provided a video file path
                        if (ex is ArgumentException || ex is FormatException)
                        {
                            dating_of_media = media[i].LastWriteTime;
                            if (ex is FormatException)
                            {
                                Log($"This file caused a FormatException: {media[i].FullName}");
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }


                    DatedMedia[i] = new Media(dating_of_media, media[i]);

                    // Update lowest and highest year found to aid in folder tree generation.
                    if (dating_of_media.Year < lowest_year) lowest_year = dating_of_media.Year;
                    if (dating_of_media.Year > highest_year) highest_year = dating_of_media.Year;

                }
            }


            // Directory tree generation.
            using (var pbar = new ProgressBar((highest_year - lowest_year) * 12, "Moving Files...", ProgressOptions))
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
                            //string dir_path = output_dir + i + @"\" + mkvp.Value; // TODO use path.combine bruh // IT ACTUALLY FUCKIN NEEDED IT BECAUSE NO SLASH BETWEEN OUT + I
                            string dir_path = Path.Combine(output_dir, i.ToString(), mkvp.Value);
                            Log($"Creating directory {dir_path}");
                            pbar.Tick($"Creating directory {dir_path} ...");
                            Directory.CreateDirectory(dir_path); // Create month's subdirectories.
                        }
                    }
                }

            }


            // Moving media files to new directory
            using (var pbar = new ProgressBar(DatedMedia.Length, "Moving Files...", ProgressOptions))
            {
                for (int i = 0; i < DatedMedia.Length; i++)
                {
                    string final_path = Path.Combine(
                        output_dir,
                        DatedMedia[i].Dating.Year.ToString(),
                        Months[DatedMedia[i].Dating.Month],
                        DatedMedia[i].File.Name
                        );

                    Log($"Moving {DatedMedia[i].File.FullName} to {final_path}");
                    pbar.Tick($"Moving {DatedMedia[i].File.Name} ...");
                    try
                    {
                        DatedMedia[i].File.MoveTo(final_path);
                    }
                    catch (IOException)
                    {
                        // TODO Log exceptions.
                        // Handle duplicates
                        if (File.Exists(final_path))
                        {
                            Log($"FILE ALREADY EXISTS EXCEPTION.");
                            Random rng = new Random();
                            string NewPath = Path.Combine(
                                output_dir,
                                DatedMedia[i].Dating.Year.ToString(),
                                Months[DatedMedia[i].Dating.Month],
                                (rng.Next(0, 1000000).ToString() + DatedMedia[i].File.Name)
                            );
                            Log($"New file name: {NewPath}");
                            DatedMedia[i].File.MoveTo(NewPath);
                        }
                        else
                        {
                            // Not a dupe, no handling available.
                            throw;
                        }
                    }
                }
            }


            DeleteEmptyDirectories(new DirectoryInfo(output_dir));

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

        private static void DeleteEmptyDirectories(DirectoryInfo directory)
        {
            List<DirectoryInfo> year_deletion_queue = new List<DirectoryInfo>();
            List<DirectoryInfo> month_deletion_queue = new List<DirectoryInfo>();

            using (var pbar = new ProgressBar(directory.GetDirectories("*", SearchOption.AllDirectories).Length, "Searching for Empty Directories...", ProgressOptions))
            {
                var dirs = directory.GetDirectories("*", SearchOption.TopDirectoryOnly);



                // year folders
                foreach (DirectoryInfo year_dir in dirs)
                {
                    var sub_dirs = year_dir.GetDirectories("*", SearchOption.TopDirectoryOnly);

                    // month folders
                    foreach (DirectoryInfo month_dir in sub_dirs)
                    {
                        if (month_dir.GetFiles("*", SearchOption.AllDirectories).Length == 0)
                        {
                            month_deletion_queue.Add(month_dir);
                        }

                    }

                    if (year_dir.GetFiles("*", SearchOption.AllDirectories).Length == 0)
                    {
                        year_deletion_queue.Add(year_dir);
                    }

                }
            }

            Log("DIRECTORIES DELETION LOG");
            // bars dont work you forgot to tick
            using (var pbar = new ProgressBar(month_deletion_queue.Count + year_deletion_queue.Count, "Deleting Empty Directories...", ProgressOptions))
            {
                foreach (DirectoryInfo dir in month_deletion_queue)
                {
                    Log($"Deleted {dir.FullName}");
                    dir.Delete();

                }


                foreach (DirectoryInfo dir in year_deletion_queue)
                {
                    Log($"Deleted {dir.FullName}");
                    dir.Delete();
                }

            }





        }


    }
}
