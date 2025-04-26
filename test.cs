using System;
using System.IO;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string startPath = @"D:\Nam4\BackEnd\EffiRent_BackEnd"; // Đặt thư mục gốc là ổ D: hoặc thư mục bạn muốn
        List<string> folders = new List<string>();
        List<string> csFiles = new List<string>();

        // Lấy danh sách thư mục và file .cs
        GetFoldersAndCsFiles(startPath, folders, csFiles);

        // In danh sách thư mục trước
        Console.WriteLine("📂 Thư mục:");
        foreach (string folder in folders)
        {
            Console.WriteLine($"    📂 {Path.GetFileName(folder)}");
        }

        // In danh sách file .cs sau
        Console.WriteLine("\n📄 File .cs:");
        foreach (string file in csFiles)
        {
            Console.WriteLine($"    📄 {Path.GetFileName(file)}");
        }
    }

    static void GetFoldersAndCsFiles(string path, List<string> folders, List<string> csFiles)
    {
        try
        {
            // Lấy danh sách thư mục
            foreach (string dir in Directory.GetDirectories(path))
            {
                folders.Add(dir);
                GetFoldersAndCsFiles(dir, folders, csFiles); // Đệ quy để duyệt qua các thư mục con
            }

            // Lấy danh sách các file .cs
            foreach (string file in Directory.GetFiles(path, "*.cs"))
            {
                csFiles.Add(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Bỏ qua các thư mục không có quyền truy cập
        }
    }
}
