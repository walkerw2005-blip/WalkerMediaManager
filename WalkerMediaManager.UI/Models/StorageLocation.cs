using System;

namespace WalkerMediaManager.UI.Models;

public sealed class StorageLocation
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int CopyCount { get; set; }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return Name.Trim();
            }

            string[] parts = [Room, Area, Shelf, Bin];
            string value = string.Join(" - ", Array.FindAll(parts, part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(value) ? "Unnamed location" : value;
        }
    }

    public string Details
    {
        get
        {
            string[] parts = [Room, Area, Shelf, Bin];
            string value = string.Join(" • ", Array.FindAll(parts, part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(value) ? "No location details recorded" : value;
        }
    }

    public string CopyCountDisplay => CopyCount == 1 ? "1 owned copy" : $"{CopyCount} owned copies";
    public string StatusDisplay => IsActive ? "Active" : "Inactive";
}
