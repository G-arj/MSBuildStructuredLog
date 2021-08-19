using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.Logging.StructuredLogger;

namespace StructuredLogViewer
{
    public class PropertiesAndItemsSearch
    {
        public IEnumerable<SearchResult> Search(
            TimedNode context,
            string searchText,
            int maxResults,
            bool markResultsInTree,
            CancellationToken cancellationToken)
        {
            var roots = new List<TreeNode>();

            if (context is Project project && project.GetRoot() is Build build)
            {
                var projectEvaluation = build.FindEvaluation(project.EvaluationId);
                if (projectEvaluation != null)
                {
                    AddPropertiesAndItems(projectEvaluation, roots);
                }
            }

            AddPropertiesAndItems(context, roots);

            static void AddPropertiesAndItems(TimedNode root, List<TreeNode> roots)
            {
                Folder properties = root.FindChild<Folder>(Strings.Properties);
                if (properties != null)
                {
                    roots.Add(properties);
                }

                Folder items = root.FindChild<Folder>(Strings.Items);
                if (items != null)
                {
                    roots.Add(items);
                }
            }

            var strings = new StringCache();
            foreach (var root in roots)
            {
                CollectStrings(root, strings);
            }

            var search = new Search(roots, strings.Instances, maxResults, markResultsInTree);
            var results = search.FindNodes(searchText, cancellationToken);
            var otherResults = new List<SearchResult>();

            // Find all folders where no other results are under that folder.
            // First find all ancestors of all non-folders.
            var allAncestors = new HashSet<BaseNode>();
            foreach (var result in results)
            {
                var node = result.Node;
                if (node is not Folder itemType)
                {
                    otherResults.Add(result);
                    foreach (var ancestor in node.GetParentChainExcludingThis())
                    {
                        allAncestors.Add(ancestor);
                    }
                }
            }

            var includeFolderChildren = new List<BaseNode>();

            // Iterate over all folders where no other results are under that folder.
            foreach (var folder in results.Select(r => r.Node).OfType<Folder>().Where(f => !allAncestors.Contains(f)))
            {
                foreach (var item in folder.Children.OfType<Item>())
                {
                    includeFolderChildren.Add(item);
                }
            }

            results =
                otherResults
                .Concat(includeFolderChildren.Select(c =>
                {
                    var result = new SearchResult(c);
                    return result;
                }))
                .ToArray();

            return results;
        }

        private void CollectStrings(BaseNode root, StringCache strings)
        {
            switch (root)
            {
                case Property property:
                    strings.Intern(property.Name);
                    strings.Intern(property.Value);
                    break;
                case AddItem addItem:
                    strings.Intern(addItem.Name);
                    break;
                case RemoveItem removeItem:
                    strings.Intern(removeItem.Name);
                    break;
                case Item item:
                    strings.Intern(item.Text);
                    break;
                case Metadata metadata:
                    strings.Intern(metadata.Name);
                    strings.Intern(metadata.Value);
                    break;
                case Folder folder:
                    strings.Intern(folder.Name);
                    break;
                default:
                    break;
            }

            if (root is TreeNode treeNode)
            {
                foreach (var child in treeNode.Children)
                {
                    CollectStrings(child, strings);
                }
            }
        }
    }
}