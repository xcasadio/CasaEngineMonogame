using Microsoft.Xna.Framework;


namespace CasaEngine.AI.Graphs
{
    public static class GraphBuilder
    {

        public static Graph<T, TK> NavGraphFromMatrixFile<T, TK>(string filename)
            where T : NavigationNode
            where TK : WeightedEdge
        {
            StreamReader file;
            int width, height, weight, xPos, yPos;
            string[] elements, separators;
            int[,] weights;
            Graph<T, TK> graph;
            List<TK> edges;
            T node;
            TK edge;

            using (file = new StreamReader(filename))
            {
                //Get the dimensions of the graph
                if (int.TryParse(file.ReadLine(), out height) == false)
                {
                    throw new Exception("Height expected");
                }

                if (int.TryParse(file.ReadLine(), out width) == false)
                {
                    throw new Exception("Width expected");
                }

                weights = new int[height, width];

                //Get the weights
                separators = new string[1];
                separators[0] = " ";
                for (int i = 0; i < height; i++)
                {
                    elements = file.ReadLine().Split(separators, StringSplitOptions.RemoveEmptyEntries);

                    if (elements.Length != width)
                    {
                        throw new Exception("Incorrect number of elements in row " + (i + 1));
                    }

                    for (int j = 0; j < elements.Length; j++)
                    {
                        if (int.TryParse(elements[j], out weight) == false)
                        {
                            throw new Exception("Weight expected");
                        }

                        weights[i, j] = weight;
                    }
                }

                //Generate all the nodes
                graph = new Graph<T, TK>(true);
                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        node = Activator.CreateInstance<T>();
                        node.Position = new Vector3(j, i, 0);
                        graph.AddNode(node);
                    }
                }

                //Generate the edges
                for (int z = 0; z < graph.ActiveNodeCount; z++)
                {
                    edges = graph.GetEdgesFromNode(z);
                    xPos = graph.GetNode(z).Index % width;
                    yPos = graph.GetNode(z).Index / width;

                    //For each node, generate the edges around it
                    for (int i = -1; i < 2; i++)
                    {
                        for (int j = -1; j < 2; j++)
                        {
                            if (i == 0 && j == 0)
                            {
                                continue;
                            }

                            //The position where the edge is going to point must be a valid position
                            if (ValidatePosition(width, height, xPos + j, yPos + i))
                            {
                                edge = Activator.CreateInstance<TK>();

                                //Set the edge values
                                edge.Start = z;
                                edge.End = xPos + j + width * (yPos + i);

                                if (weights[yPos + i, xPos + j] == -1)
                                {
                                    edge.Cost = float.MaxValue;
                                }

                                else
                                {
                                    edge.Cost = weights[yPos + i, xPos + j];
                                }

                                edges.Add(edge);
                            }
                        }
                    }
                }
            }

            return graph;
        }



        private static bool ValidatePosition(int width, int height, int x, int y)
        {
            if (x < 0 || x >= width)
            {
                return false;
            }

            if (y < 0 || y >= height)
            {
                return false;
            }

            return true;
        }

    }
}
