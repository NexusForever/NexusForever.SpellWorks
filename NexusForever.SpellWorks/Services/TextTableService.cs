﻿using System.IO;
using MahApps.Metro.Controls.Dialogs;
using Nexus.Archive;
using NexusForever.SpellWorks.GameTable;

namespace NexusForever.SpellWorks.Services
{
    public class TextTableService : ITextTableService
    {
        private TextTable currentTextTable;

        #region Dependency Injection

        private readonly IArchiveService _archiveService;

        public TextTableService(
            IArchiveService archiveService)
        {
            _archiveService = archiveService;
        }

        #endregion

        private int count;

        public async Task Initialise(ProgressDialogController controller)
        {
            controller.SetMessage("Loading Text Tables...");
            controller.Minimum = 0;
            controller.Maximum = _archiveService.LocalisationArchives.Count;

            List<Task> tasks = [];
            foreach (Archive archive in _archiveService.LocalisationArchives)
            {
                foreach (IArchiveFileEntry file in archive.IndexFile.GetFiles("*.bin"))
                {
                    tasks.Add(LoadTextTable(controller, archive, file));
                }
            }

            await Task.WhenAll(tasks);
        }

        private Task<TextTable> LoadTextTable(ProgressDialogController controller, Archive archive, IArchiveFileEntry file)
        {
            return Task.Run(() =>
            {
                using Stream archiveStream = archive.OpenFileStream(file);
                using var memoryStream = new MemoryStream();
                archiveStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var textTable = new TextTable(memoryStream);
                Interlocked.Increment(ref count);
                controller.SetProgress(count);

                // TODO: fix me
                currentTextTable = textTable;
                return textTable;
            });
        }

        public string GetText(uint id)
        {
            return currentTextTable.GetEntry(id) ?? "UNKNOWN LOCALISED TEXT ID";
        }
    }
}
