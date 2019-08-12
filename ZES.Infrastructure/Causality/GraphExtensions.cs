using System.Collections.Generic;
using System.Linq;
using Frontenac.Blueprints;
using VelocityGraph;

namespace ZES.Infrastructure.Causality
{
    /// <summary>
    /// Velocity graph extensions
    /// </summary>
    public static class GraphExtensions
    {
        /// <summary>
        /// Gets the path between the vertices
        /// </summary>
        /// <param name="from">Originating vertex</param>
        /// <param name="edgeType">Edge type</param>
        /// <param name="to">Ending vertex</param>
        /// <returns>List of vertices representing the path between the nodes</returns>
        public static IEnumerable<Vertex> GetPath(this Vertex from, EdgeType edgeType, Vertex to = null)
        {
            var vertices = new List<Vertex> { from };

            var descendants = from.Traverse(Direction.Out, new HashSet<EdgeType> { edgeType });
            while (descendants.Count > 0)
            {
                from = descendants.FirstOrDefault().Key;
                vertices.Add(from);
                if (from.VertexId == to?.VertexId)
                    break;
                
                descendants = from.Traverse(Direction.Out, new HashSet<EdgeType> { edgeType }); 
            }

            return vertices;
        }

        /// <summary>
        /// Gets final leaf of the graph path starting from originating vertex
        /// </summary>
        /// <param name="from">Originating vertex</param>
        /// <param name="edgeType">Edge type (normally stream) </param>
        /// <returns>Final vertex</returns>
        public static Vertex End(this Vertex from, EdgeType edgeType)
        {
            return from.GetPath(edgeType).LastOrDefault();
        }
    }
}