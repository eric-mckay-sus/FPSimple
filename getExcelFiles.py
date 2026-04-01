import glob
import os
import shutil
from sys import argv

def main():
    """Initialize the source & destination directories, then delegate to copy_filetype_recursive"""
    source_directory = r'P:\PE III\1-Standard Documents\FPM' # Default source
    destination_directory = r'C:\LOCAL NETWORK FILES\XLS' # Default dest
    if len(argv) > 1: # The first non-program name argument is the source
        source_directory = argv[1]
    if len(argv) > 2: # The second is the dest
        destination_directory = argv[2]

    extension_to_find = '*.xls'

    copy_filetype_recursive(source_directory, destination_directory, extension_to_find)

def copy_filetype_recursive(source_dir, destination_dir, file_extension):
    """
    Copies all files of a specific type from source_dir and its subdirectories
    to a single destination_dir.

    Args:
        source_dir (str): The root directory to start searching from.
        destination_dir (str): The directory where files will be copied.
        file_extension (str): The file extension to filter (e.g., '*.jpg', '*.txt').
    """

    # Ensure the destination directory exists; create it if not
    if not os.path.exists(destination_dir):
        os.makedirs(destination_dir)
        print(f"Created destination directory: {destination_dir}")

    # Use glob with recursive=True to find all files with the specified extension
    # The "**" pattern matches all files and zero or more subdirectories
    search_pattern = os.path.join(source_dir, '**', file_extension)
    files_to_copy = glob.glob(search_pattern, recursive=True)

    if not files_to_copy:
        print(f"No files with extension {file_extension} found in {source_dir}")
        return

    print(f"Found {len(files_to_copy)} files to copy.")

    # Iterate over the found files and copy each one
    for file_path in files_to_copy:
        # Get the filename (basename) to avoid copying the entire directory structure
        file_name = os.path.basename(file_path)
        destination_path = os.path.join(destination_dir, file_name)

        # Handle potential duplicate filenames in the destination
        if os.path.exists(destination_path):
            print(f"Warning: '{file_name}' already exists in destination. Overwriting.")

        try:
            # Use shutil.copy2 to copy the file and its metadata (like timestamps)
            shutil.copy2(file_path, destination_path)
            print(f"Copied: {file_name}")
        except IOError as e:
            print(f"Error copying {file_name}: {e}")

if __name__ == "__main__":
    main()
