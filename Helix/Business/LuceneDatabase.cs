using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Directory = Lucene.Net.Store.Directory;
using Version = Lucene.Net.Util.Version;

namespace Helix
{
    class LuceneDatabase : IDisposable
    {
        Directory _indexDirectory;
        readonly IndexSearcher _indexSearcher;
        readonly IndexWriter _indexWriter;
        readonly StandardAnalyzer _standardAnalyzer;
        static readonly object StaticLock = new object();
        static readonly string WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        public LuceneDatabase()
        {
            EnsureLuceneIndexDirectoryRecreated();
            EnsureLuceneIndexDirectoryUnlocked();

            _standardAnalyzer = new StandardAnalyzer(Version.LUCENE_30);
            _indexWriter = new IndexWriter(_indexDirectory, _standardAnalyzer, IndexWriter.MaxFieldLength.UNLIMITED);
            _indexSearcher = new IndexSearcher(_indexDirectory, true);
        }

        public void Dispose()
        {
            _standardAnalyzer?.Close();
            _indexSearcher?.Dispose();
            _indexWriter?.Dispose();
            _indexDirectory?.Dispose();
        }

        public void Optimize()
        {
            lock (StaticLock)
            {
                var keywordAnalyzer = new KeywordAnalyzer();
                using (var indexWriter = new IndexWriter(_indexDirectory, keywordAnalyzer, IndexWriter.MaxFieldLength.UNLIMITED))
                {
                    keywordAnalyzer.Close();
                    indexWriter.Optimize();
                    indexWriter.Dispose();
                }
            }
        }

        public bool TryIndex(string content)
        {
            lock (StaticLock)
            {
                if (!string.IsNullOrWhiteSpace(SearchFor(content))) return false;

                var document = new Document();
                document.Add(new Field("content", content, Field.Store.YES, Field.Index.NOT_ANALYZED));
                _indexWriter.AddDocument(document);
                _indexWriter.Commit();

                return true;
            }
        }

        void EnsureLuceneIndexDirectoryRecreated()
        {
            var luceneIndexDirectoryPath = Path.Combine(WorkingDirectory, "lucene_index");
            if (System.IO.Directory.Exists(luceneIndexDirectoryPath)) System.IO.Directory.Delete(luceneIndexDirectoryPath, true);
            _indexDirectory = FSDirectory.Open(System.IO.Directory.CreateDirectory(luceneIndexDirectoryPath));
        }

        void EnsureLuceneIndexDirectoryUnlocked()
        {
            var luceneLockFilePath = Path.Combine(WorkingDirectory, "write.lock");
            if (IndexWriter.IsLocked(_indexDirectory)) IndexWriter.Unlock(_indexDirectory);
            if (File.Exists(luceneLockFilePath)) File.Delete(luceneLockFilePath);
        }

        Query ParseQuery(string searchQuery)
        {
            Query query;
            var queryParser = new QueryParser(Version.LUCENE_30, "content", _standardAnalyzer);

            try
            {
                query = queryParser.Parse(searchQuery.Trim());
            }
            catch (ParseException)
            {
                query = queryParser.Parse(QueryParser.Escape(searchQuery.Trim()));
            }

            return query;
        }

        string SearchFor(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || !_indexDirectory.ListAll().Any()) return null;
            var query = ParseQuery(content);
            var hits = _indexSearcher.Search(query, 1).ScoreDocs;
            return hits.Any() ? _indexSearcher.Doc(hits[0].Doc).Get("content") : null;
        }
    }
}