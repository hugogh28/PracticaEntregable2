using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

/******************************************************************************
* GRADO EN DISEÑO Y DESARROLLO DE VIDEOJUEGOS - ANIMACIÓN 3D
* Bloque 2 - Práctica Entregable 1
*
* Nombre y apellidos: Hugo García Hernández
* DNI: 03212391G
* Curso académico: 2025-2026
*
* Nombre de la clase: MassSpring
* Breve descripción: La siguiente clase de C# gestiona todos los cálculos que debene efectuarse en el objeto masa-muelle, para así obtener unas físicas adecuadas.
* Esto se logra desde la asignación de nodos y muelles al mallado del objeto, hasta la aplicación de los métodos de integración Euler explícito, el cual, debido a su 
* inestabilidad, no es recomendable usarlo, y Euler simpléctico.
*****************************************************************************/

public class MassSpring : MonoBehaviour
{
    [Header("Archivos")]
    public TextAsset nodeTetgen; //Tomamos el archivo de texto que contiene la información de los vértices
    public TextAsset eleTetgen; //Tomamos el archivo de texto que contiene la información de los tetraedros

    [Header("Modificadores de la animación")]
    public bool paused; //Booleano que nos servirá para pausar la animación
    public float densityMass; //Masa del objeto (100 gramos)
    public Vector3 g; //El valor de la gravedad aplicado al objeto masa-muelle (está en m/s)
    public float dampingNodes; //Amortiguamiento para el movimiento absoluto de los nodos
    public float dampingSprings; //Amortiguamiento para frenar la deformación de los muelles

    public enum Integration //Los diferentes métodos de integración disponibles
    {
        ExplicitEuler = 0,
        SymplecticEuler = 1
    }

    private enum TypeOfFile
    {
        Node = 0,
        Ele = 1
    }

    private TypeOfFile node = TypeOfFile.Node;
    private TypeOfFile ele = TypeOfFile.Ele;

    [Header("Métodos de integración")]
    public Integration integrationMethod; //Este será el método de integración escogido

    [Header("Paso de integración")]
    public float h; //El paso de integración (cuanto más rápido sea, más inestable puede ser)

    public List<Spring> ListOfSprings; //Lista de muelles
    bool _springListIsFull = false; //Booleano para comprobar si la lista de muelles está llena
    bool _nodeListIsFull = false; //Booleano para comprobar si la lista de nodos está llena

    public List<Node> ListOfNodes; //Lista de nodos

    private List<int> _listOfTet = new List<int>();
    private List<float> _listOfNode = new List<float>();
    private List<Vector4> _listOfTetVector = new List<Vector4>();
    private List<Vector3> _listOfNodeVector = new List<Vector3>();

    bool nodeFull = false;

    [Header("Constantes de rigidez")]
    public float kT; //Constante de rigidez de los muelles de tracción
    public float kF; //Constante de rigidez de los muelles de flexión

    [Header("Fijadores")]
    public List<Fixer> fixer = new List<Fixer>(); //Desde Unity se hará por esta línea la asignación del fixer, es decir, del cubo que fija nodos, a este script para que los nodos se fijen

    Mesh cloth;

    Vector3[] verts;

    Vector3Int[] _edgesToDraw; //Creamos un array edges de tipo Vector3Int (para ahorrarnos el casting de float a int), para asignar las aristas 
    Vector3Int[] _edges; //Creamos un array edges de tipo Vector3Int (para ahorrarnos el casting de float a int), para asignar las aristas 

    Vector3Int[] _faces; //Creamos un array faces de tipo Vector3Int para almacenar todos los triángulos de los tetraedros

    Dictionary<Vector3Int, Vector3Int> _facesDictionary = new Dictionary<Vector3Int, Vector3Int>(); //Se crea un diccionario con el objetivo de filtrar más fácilmente los triángulos repetidos, ya que se les puede asignar una clave
    Dictionary<Vector2Int, Vector3Int> _edgesToDrawDictionary = new Dictionary<Vector2Int, Vector3Int>(); //Se crea un diccionario con el objetivo de filtrar más fácilmente las aristas repetidas, ya que se les puede asignar una clave
    Dictionary<Vector2Int, Vector3Int> _edgesDictionary = new Dictionary<Vector2Int, Vector3Int>(); //Se crea un diccionario con el objetivo de filtrar más fácilmente las aristas repetidas, ya que se les puede asignar una clave
    Dictionary<Vector2Int, float> _edgesVolumeDictionary = new Dictionary<Vector2Int, float>();

    List<Vector3Int> _facesList = new List<Vector3Int>();    //Lista de caras no duplicadas
    List<Vector3Int> _edgesToDrawList = new List<Vector3Int>();  //Lista de aristas que aparecerán dibujadas
    List<Vector3Int> _edgesList = new List<Vector3Int>();    //Lista de aristas no duplicadas para el cálculo de físicas
    List<float> _edgesVolume = new List<float>();

    List<float> _vTet = new List<float>();  //Lista que almacena los volúmenes de todos los tetraedros

    List<float> _volumes;

    float[] _masses;

    List<Vector4> _weigths = new List<Vector4>();
    List<Vector3> _bNodes = new List<Vector3>(); //Lista que guarda las posiciones baricéntricas de los vértices de la malla
    List<int> _containedTet = new List<int>();

    void Start()
    {
        Debug.Log("Inicio del archivo .node");
        ParseFile(node, nodeTetgen, _listOfNode);

        Debug.Log("Inicio del archivo .ele");
        ParseFile(ele, eleTetgen, _listOfTet);

        TurnIntoVectors(_listOfNode, _listOfNodeVector, _listOfTet, _listOfTetVector);

        nodeFull = true;

        Mesh mesh = this.GetComponentInChildren<MeshFilter>().mesh; //Se guarda en la variable mesh el mallado del objeto

        cloth = mesh; //Para poder hacer las modificaciones en la malla, se guarda la mesh en una variable global

        Vector3[] vertices = mesh.vertices; //Se guardan en un array todos los vértices de la mesh

        verts = vertices; //Para poder hacer las modificaciones en la mesh, se guardan los vértices de la mesh en una variable global


        List<Node> nodes = new List<Node>(vertices.Length); //Se crea una lista de nodos cuyo tamaño sea el de los vértices de la mesh
        List<Spring> springs = new List<Spring>(); //Se crea una lista de muelles cuyo tamaño es indefinido (ya que se presupone que podemos usar cualquier bandera)
        List<Spring> springsF = new List<Spring>(); //Se crea una lista de muelles cuyo tamaño es indefinido (ya que se presupone que podemos usar cualquier bandera)

        int[] triangles = mesh.triangles; //Se guardan en un array todos los triángulos de la mesh

        for(int i = 0; i + 2 < _listOfNode.Count; i += 3) //Se itera tantas veces como vértices hay en el array vertices, complejidad O(n)
        {
            nodes.Add(new Node(new Vector3(_listOfNode[i], _listOfNode[i + 1], _listOfNode[i + 2]), fixer, transform)); //Cada vez que se itera sobre el bucle de vértices de la mesh, se añade un nuevo nodo, cuya posición corresponde a la de su vértice
                                                     //Además, se comprueba, mediante la lista de fixers, si dicho nodo debe estar fijado antes de comenzar la animación y, para dicha comprobació,
                                                     //es necesario reconvertir de coordenadas locales a globales, por lo que pasamos el componente transform del objeto masa-muelle al constructor del nodo
            
            //verts[i] = nodes[i].pos; //Se rellena el array verts con sus correspondientes nodos del array nodes
        }

        ListOfNodes = nodes; //Para poder hacer uso de OnDrawGizmos() se pasa la lista nodes a ListOfNodes

        _nodeListIsFull = true; //Se activa el booleano nodeListIsFull cuando la lista de nodos se ha llenado con todos los elementos del objeto

        CalculateWeights(verts, _listOfTetVector, _listOfNodeVector);

        _volumes = CalculateVolumes(_listOfNode, _listOfTet); //Calculamos los volúmenes de todos los tetraedros
        CalculateEdgeVolumes(); //Calculos los volúmenes de todas las aristas justo después de calcular los de los tetraedros

        _masses = new float[nodes.Count];

        for(int i = 0; i<_listOfTet.Count; i += 4)
        {
            float tetMass = densityMass * _volumes[i / 4];

            //Añadimos al array de masas la masa del nodo de cada tetraedro
            _masses[_listOfTet[i] - 1] += tetMass / 4f;
            _masses[_listOfTet[i + 1] - 1] += tetMass / 4f;
            _masses[_listOfTet[i + 2] - 1] += tetMass / 4f;
            _masses[_listOfTet[i + 3] - 1] += tetMass / 4f;
        }

        _faces = new Vector3Int[_listOfTet.Count]; //Creamos un array de caras, para almacenar todos los triángulos de todos los tetraedros

        for(int i = 0; i + 3<_faces.Length; i += 4) //Seguimos una adición de triángulos (faces) de forma similar al objeto MassSpringCloth
        {
            //Se debe restar 1 al índice obtenido de la lista de tetraedros porque Tetgen comienza la numeración en 1 y no en 0
            _faces[i] = new Vector3Int(_listOfTet[i] - 1, _listOfTet[i + 1] - 1, _listOfTet[i + 2] - 1);        //ABC
            _faces[i+1] = new Vector3Int(_listOfTet[i] - 1, _listOfTet[i + 1] - 1, _listOfTet[i + 3] - 1);      //ABD
            _faces[i+2] = new Vector3Int(_listOfTet[i] - 1, _listOfTet[i + 2] - 1, _listOfTet[i + 3] - 1);      //ACD
            _faces[i+3] = new Vector3Int(_listOfTet[i + 1] - 1, _listOfTet[i + 2] - 1, _listOfTet[i + 3] - 1);  //BCD
        }

        _edges = new Vector3Int[_faces.Length * 3]; //Creamos la estructura edges, para almacenar todas las aristas

        for (int i = 0; i< _faces.Length; i++) //Iteramos sobre el array de todos los triángulos para extraer los duplicados
        {
            if (_facesDictionary.ContainsKey(GetFaceKey(_faces[i])))
            {
                _facesDictionary.Remove(GetFaceKey(_faces[i])); //Eliminamos la cara interna al encontrar un duplicado
            }
            else
            {
                _facesDictionary.Add(GetFaceKey(_faces[i]), _faces[i]); //Si la clave no existe, se añade el triángulo al diccionario
            }

            _edges[i * 3] = new Vector3Int(Mathf.Min(_faces[i].x, _faces[i].y), Math.Max(_faces[i].x, _faces[i].y), _faces[i].z);
            _edges[i * 3 + 1] = new Vector3Int(Mathf.Min(_faces[i].x, _faces[i].z), Math.Max(_faces[i].x, _faces[i].z), _faces[i].y);
            _edges[i * 3 + 2] = new Vector3Int(Mathf.Min(_faces[i].y, _faces[i].z), Math.Max(_faces[i].y, _faces[i].z), _faces[i].x);
        }
        _facesList = _facesDictionary.Values.ToList();

        _edgesToDraw = new Vector3Int[_facesList.Count * 3]; //Creamos la estructura edgesToDraw, para almacenar las aristas sin las caras internas

        for (int i = 0; i<_facesList.Count; i++) //Recorremos la lista de triángulos, para asignar a edges cada arista
        {
            _edgesToDraw[i*3] = new Vector3Int(Math.Min(_facesList[i].x, _facesList[i].y), Math.Max(_facesList[i].x, _facesList[i].y), _facesList[i].z);    // ABC
            _edgesToDraw[i*3+1] = new Vector3Int(Math.Min(_facesList[i].x, _facesList[i].z), Math.Max(_facesList[i].x, _facesList[i].z), _facesList[i].y);  // ACB
            _edgesToDraw[i*3+2] = new Vector3Int(Math.Min(_facesList[i].y, _facesList[i].z), Math.Max(_facesList[i].y, _facesList[i].z), _facesList[i].x);  // BCA 
        }

        for(int i = 0; i<_edges.Length; i++)
        {
            if (_edgesDictionary.ContainsKey(GetEdgeKey(_edges[i])))
            {
                continue;
            }
            else
            {
                _edgesDictionary.Add(GetEdgeKey(_edges[i]), _edges[i]);
                _edgesList.Add(_edges[i]);
            }
        }

        for (int i = 0; i< _edgesToDraw.Length; i++) //Se sigue un método de identificación de duplicados de forma similar que con faces, solo que en este caso, debemos comprobar solo entre dos valores
        {
            if (_edgesToDrawDictionary.ContainsKey(GetEdgeKey(_edgesToDraw[i])))
            {
                continue; //Saltamos la iteración para ignorar los duplicados
            }
            else
            {
                _edgesToDrawDictionary.Add(GetEdgeKey(_edgesToDraw[i]), _edgesToDraw[i]); //Si no hay arista duplicada se añade al diccionario
                _edgesToDrawList.Add(_edgesToDraw[i]);    //Si no hay arista duplicada, se añade a la lista para dibujarla después
            }
        }

        /*for(int i = 0; i < edges.Length; i++) //Se itera tantas veces como aristas hay (600 en el caso original)
        {

            if (i<edges.Length-1&&edges[i].x == edges[i + 1].x && edges[i].y == edges[i + 1].y) //Si dos aristas (adyacentes en la lista) se detectan como duplicadas, se añadirá un nodo de flexión y se evitará añadir un muelle de tracción
            {
                springs.Add(new Spring(kT, nodes[edges[i].x], nodes[edges[i].y])); //Se añade un nodo de tracción en la arista compartida entre nodos opuestos de triángulos adyacentes

                i++; //Saltamos una posición para evitar duplicar muelles
            }
            else
            {
                springs.Add(new Spring(kT, nodes[edges[i].x], nodes[edges[i].y])); //Añade un muelle de tracción entre los vértices de la arista
            }
        }*/

        for(int i = 0; i<_edgesList.Count; i++)
        {
            float vol = _edgesVolumeDictionary[GetEdgeKey(_edgesList[i])];

            //En lugar de añadir solo los nodos y la constante de rigidez, se añade también el volumen de la arista (calculado en CalculateEdgeVolumes)
            springs.Add(new Spring(kT, nodes[_edgesList[i].x], nodes[_edgesList[i].y], vol));
        }

        _springListIsFull = true; //Se activa el booleano springListIsFull cuando la lista de muelles se ha llenado con todos los elementos del objeto

        ListOfSprings = springs; //Para poder hacer uso de OnDrawGizmos() se pasa la lista springs a ListOfSprings
    }

    //Método genérico para calcular las posiciones baricéntricas de cada vértice de la malla
    Vector3 BaricentricPosition(Vector4 weight, Vector4 tet)
    {
        Vector3 a = ListOfNodes[(int)tet.x].pos;
        Vector3 b = ListOfNodes[(int)tet.y].pos;
        Vector3 c = ListOfNodes[(int)tet.z].pos;
        Vector3 d = ListOfNodes[(int)tet.w].pos;

        return weight.x * a + weight.y * b + weight.z * c + weight.w * d;
    }

    //Método genérico que, dados dos puntos de una arista (y un sexto del volumen del tetraedro en el que se encuentran) comprueban 
    //si el diccionario tiene un registro de la arista, se añadirá el volumen del nuevo tetraedro (para evitar pérdidas importantes para los cálculos)
    void AddVolumeToEdgeDictionary(int x, int y, float sixthOfVolume)
    {
        if (_edgesVolumeDictionary.ContainsKey(GetEdgeKey(new Vector3Int(x, y, 0)))) //Usamos un 0 por rellenar el hueco, ya que dado el método GetEdgeKey se podría usar cualquier número ya que se ignora
        {
            _edgesVolumeDictionary[GetEdgeKey(new Vector3Int(x, y, 0))] += sixthOfVolume;
        }
        else
        {
            _edgesVolumeDictionary.Add(GetEdgeKey(new Vector3Int(x, y, 0)), sixthOfVolume);
        }
    }

    void CalculateEdgeVolumes()
    {
        for(int i = 0; i < _listOfTet.Count; i += 4)
        {
            int a = _listOfTet[i] - 1;
            int b = _listOfTet[i + 1] - 1;
            int c = _listOfTet[i + 2] - 1;
            int d = _listOfTet[i + 3] - 1;

            AddVolumeToEdgeDictionary(a, b, _volumes[i / 4] / 6f);  //Arista AB
            AddVolumeToEdgeDictionary(a, c, _volumes[i / 4] / 6f);  //Arista AC
            AddVolumeToEdgeDictionary(a, d, _volumes[i / 4] / 6f);  //Arista AD
            AddVolumeToEdgeDictionary(b, c, _volumes[i / 4] / 6f);  //Arista BC
            AddVolumeToEdgeDictionary(b, d, _volumes[i / 4] / 6f);  //Arista BD
            AddVolumeToEdgeDictionary(c, d, _volumes[i / 4] / 6f);  //Arista CD
        }
    }

    //Método que calcula las coordenadas baricéntricas de todos los vértices del mallado
    void CalculateWeights(Vector3[] meshVerts, List<Vector4> tet, List<Vector3> nodes)
    {
        for(int i = 0; i< meshVerts.Length;i++)
        {
            for(int j = 0; j<tet.Count; j++)
            {
                //Por cada índice de la malla calculamos los pesos de cada tetraedro, si la suma de todos es 1 (y no más ni menos), el vértice pertenece a ese tetraedro
                float volumeTotal = CalculateVolume(nodes[(int)tet[j].x], nodes[(int)tet[j].y], nodes[(int)tet[j].z], nodes[(int)tet[j].w]);

                float wA = CalculateVolume(meshVerts[i], nodes[(int)tet[j].y], nodes[(int)tet[j].z], nodes[(int)tet[j].w]) / volumeTotal;
                float wB = CalculateVolume(nodes[(int)tet[j].x], meshVerts[i], nodes[(int)tet[j].z], nodes[(int)tet[j].w]) / volumeTotal;
                float wC = CalculateVolume(nodes[(int)tet[j].x], nodes[(int)tet[j].y], meshVerts[i], nodes[(int)tet[j].w]) / volumeTotal;
                float wD = CalculateVolume(nodes[(int)tet[j].x], nodes[(int)tet[j].y], nodes[(int)tet[j].z], meshVerts[i]) / volumeTotal;

                float w = wA + wB + wC + wD; //Sumamos todos los pesos

                if (Mathf.Abs(w-1) < 0.0001f ) //Usamos Epsilon porque debido al uso de float, podría haber casos en los que w-1 no sea exactamente 0
                {
                    _weigths.Add(new Vector4(wA,wB,wC,wD));
                    //_bNodes.Add(BaricentricPosition(_weigths[i], tet[j]));
                    _containedTet.Add(j);
                    break; //Una vez se encuentra el tetraedro que contiene al vértice, salimos del bucle para no comprobar más tetraedros para ese vértice
                }
            }
        }
    }

    //Método genérico que obtiene el volumen de un tetraedro de los puntos dados por parámetro
    float CalculateVolume(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        return Mathf.Abs(Vector3.Dot(b - a, Vector3.Cross(c - a, d - a))) / 6f;
    }

    //Método que obtiene los volúmenes de todos los tetraedros
    List<float> CalculateVolumes(List<float> nodes, List<int> tet)
    {
        List<float> volume = new List<float>();
        List<Vector3> n = new List<Vector3>();

        for (int i = 0; i < nodes.Count; i += 3)
        {
            n.Add(new Vector3(nodes[i], nodes[i + 1], nodes[i + 2]));
        }

        for(int i = 0; i<tet.Count; i+=4)
        {
            Vector3 a = n[tet[i] - 1];
            Vector3 b = n[tet[i + 1] - 1];
            Vector3 c = n[tet[i + 2] - 1];
            Vector3 d = n[tet[i + 3] - 1];

            volume.Add(
                Math.Abs(Vector3.Dot(b - a, Vector3.Cross(c - a, d - a))) / 6f
                );
        }

        return volume;
    }

    void TurnIntoVectors(List<float> nodes, List<Vector3> nodesV, List<int> tet, List<Vector4> tetV)
    {
        //nodesV.Clear();
        //tetV.Clear();

        for(int i=0; i<nodes.Count; i+=3)
        {
            nodesV.Add(new Vector3(nodes[i], nodes[i + 1], nodes[i + 2]));
        }for(int i = 0; i< tet.Count; i += 4)
        {
            tetV.Add(new Vector4(tet[i] - 1, tet[i + 1] - 1, tet[i + 2] - 1, tet[i + 3] - 1));
        }
    }

    Vector2Int GetEdgeKey(Vector3Int edge)
    {
        int min = Mathf.Min(edge.x, edge.y);    //Extraemos el índice más pequeño de los dos
        int max = Mathf.Max(edge.x, edge.y);    //Extraemos el índice más grande de los dos
        return new Vector2Int(min, max);
    }

    Vector3Int GetFaceKey(Vector3Int face)
    {
        int min = Mathf.Min(face.x, Mathf.Min(face.y, face.z));    //Extraemos el índice más pequeño de los tres
        int max = Mathf.Max(face.x, Mathf.Max(face.y, face.z));    //Extraemos el índice más grande de los tres
        int mid = face.x + face.y + face.z - min - max;            //Extraemos el índice medio al haber obtenido los otros dos, simplemente restando al total el valor del mínimo y del máximo
        return new Vector3Int(min, mid, max);
    }

    //Se pasa por referencia el tipo de archivo, para poder deyterminar cómo se parseará
    void ParseFile<T>(TypeOfFile type,TextAsset file, List<T> list)
    {
        string text = file.text; //Extraemos todo el contenido del archivo, para ello, usamos el componente "text" 
        text = text.Substring(0, text.IndexOf("#")); //Eliminamos todo lo que se encuentre en el string al aparecer el #, de este modo, se elimina la línea comentada
        char[] separators = {' ', '\n', '\r', '\t'}; //Saltamos espacios, saltos de línea o tabulaciones 
        string[] strValues = text.Split(separators, StringSplitOptions.RemoveEmptyEntries); //Para evitar posibles espacios en blanco
        
        int idx;
        int stride;

        if(type == TypeOfFile.Node) 
        {
            idx = 4;
            stride = 4;

            for (int i = idx; i < strValues.Length; i += stride) //Saltamos los número iniciales, ya que los índices no se busca conservarlos
            {
                AddValues(i+1, strValues, list);
                AddValues(i+3, strValues, list); //Ordenamos los nodos, ya que TetGen y Unity no siguen la misma colocación de ejes
                AddValues(i+2, strValues, list);
            }
        }
        else if(type == TypeOfFile.Ele)
        {
            idx = 3;
            stride = 5;

            for (int i = idx; i < strValues.Length; i++) //Saltamos los número iniciales, ya que los índices no se busca conservarlos
            {
                if (i == idx)
                {
                    continue; //Saltamos esta iteración, pues no es necesario comprobar más
                }

                if ((i - idx) % stride != 0) //Debemos restar idx a i, porque a diferencia de con el fichero node, idx y stride no son el mismo valor y no valdría solo con comprobar si i % idx != 0
                {
                    AddValues(i, strValues, list);
                }
            }
        }
    }

    //Método genérico para añadir los valores de cada fichero a la lista pasada como parámetro
    private void AddValues<T>(int idx, string[] values, List<T> list)
    {
        object value;
        if(typeof(T) == typeof(float))
        {
            value = float.Parse(values[idx], System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            value = int.Parse(values[idx], System.Globalization.CultureInfo.InvariantCulture);
        }
        list.Add((T)value);
        Debug.Log(value);
    }

    private void OnDrawGizmos()
    {
        //Dibujado de los gizmos de los nodos en coordenadas globales
        Gizmos.matrix = transform.localToWorldMatrix; //Se hace el paso de coordenadas locales a globales para evitar que los gizmos se pinten en otro lugar que no sea el nodo correspondiente
        DrawIfNotNull(); //Se trata de dibujar los gizmos
    }

    //Para evitar llamadas a elementos no existentes usamos el método DrawIfNotNull, que comprueba si las listas de nodos y muelles han sido inizialidas y llenadas para evitar
    //rellenar Gizmos que no existen
    void DrawIfNotNull()
    {
        /*Gizmos.color = Color.blue;
        for (int i = 0; i + 2 < ListOfNode.Count; i += 3)
        {
            Vector3 pos = new Vector3(ListOfNode[i], ListOfNode[i + 1], ListOfNode[i + 2]);
            Gizmos.DrawSphere(pos, 0.2f);
        }*/

        if (_nodeListIsFull)
        {
            Gizmos.color = Color.blue; //Se asigna color azul a los gizmos esféricos de los nodos
            foreach (var node in ListOfNodes) //Se recorre cada nodo de la lista
            {
                Gizmos.DrawSphere(node.pos, 0.4f); //Se pinta una esfera de radio 0.2 (unidades de Unity) sobre cada nodo de la lista
            }
        }



        if (_springListIsFull)
        {
            /*Gizmos.color = Color.red;
            foreach (var spring in ListOfSprings) //Se recorre cada muelle de la lista
            {
                if (spring.k == kT) //Si es un muelle de tracción, se pinta de rojo. kT es la constante de rigidez de un muelle de tracción
                {
                    Gizmos.DrawLine(spring.nodeA.pos, spring.nodeB.pos); //Se pinta una línea sobre cada muelle de la lista
                }
            }

            Gizmos.color = Color.blue;
            foreach (var spring in ListOfSprings) //Se recorre cada muelle de la lista
            {
                if (spring.k == kF) //Si es un muelle de flexión, se pinta de azul. kF es la constante de rigidez de un muelle de flexión
                {
                    Gizmos.DrawLine(spring.nodeA.pos, spring.nodeB.pos); //Se pinta una línea sobre cada muelle de la lista
                }
            }*/
            Gizmos.color = Color.red;
            foreach (var edge in _edgesToDrawList)
            {
                Gizmos.DrawLine(ListOfNodes[edge.x].pos, ListOfNodes[edge.y].pos);
            }

            //Para testeo, dibujado de todas las aristas no duplicadas
            /*Gizmos.color = Color.green;
            foreach (var edge in edgesList)
            {
                Gizmos.DrawLine(new Vector3(ListOfNode[edge.x * 3], ListOfNode[edge.x * 3 + 1], ListOfNode[edge.x * 3 + 2]), new Vector3(ListOfNode[edge.y * 3], ListOfNode[edge.y * 3 + 1], ListOfNode[edge.y * 3 + 2]));
            }*/
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.P)) // Detectamos si se ha pulsado la tecla P
        {
            // La tecla P hace de "toggle" para pausar o quitar la pausa de la
            // animación
            paused = !paused;
        }
    }

    private void FixedUpdate()
    {
        if (paused)
            // Si está pausada la animación, no hacemos nada y regresamos
            return;

        // Según el método de integración escogido, se invoca una función u otra
        switch (integrationMethod)
        {
            case Integration.ExplicitEuler:
                integrateExplicitEuler();
                break;

            case Integration.SymplecticEuler:
                integrateSymplecticEuler();
                break;
            default:
                print("ERROR METODO INTEGRACION DESCONOCIDO");
                break;
        }

        // Recorremos la lista de muelles para recalcularlos, una vez que hemos
        // calculado la nueva posición de los nodos con el método de integración
        foreach (Spring spring in ListOfSprings)
        {
            // Vector dirección del muelle, apunta de B a A            
            spring.u = spring.nodeA.pos - spring.nodeB.pos;
            // Nueva longitud del muelle 
            spring.length = spring.u.magnitude;
            // Normalizamos el vector que almacena la orientación del muelle
            spring.u = Vector3.Normalize(spring.u);
            // Posición del punto medio del muelle: media aritmética de las
            // posiciones de los dos nodos
            spring.pos = (spring.nodeA.pos + spring.nodeB.pos) / 2f;
            // Orientamos correctamente el muelle según el vector dir
            spring.rotation = Quaternion.FromToRotation(Vector3.up, spring.u);
        }
    }

    /// <summary>
    /// Método de integración de Euler Explícito
    /// </summary>
    void integrateExplicitEuler()
    {
        Vector3 gravity = transform.InverseTransformDirection(g); //Reconvertimos el vector de la gravedad para que la tela caiga hacia el suelo (y no hacia su eje y local)
        // Recorremos la lista de nodos para aplicar las fuerzas a cada uno de
        // ellos
        for (int i = 0; i < ListOfNodes.Count; i++)
        {
            Node node = ListOfNodes[i];
            if (!node.fixedNode) // Si el nodo no es fijo
            {
                // r_(n+1) = r_n + h * v_n

                node.pos += h * node.vel;
                //verts[i] = node.pos; //Asignamos el nuevo valor de la posición del nodo al array verts
                node.force = -(_masses[i]) * gravity;


                node.force -= (dampingNodes) * node.vel; //Frenamos el movimiento absoluto de los nodos

            }
        }
        for (int j = 0; j<verts.Length;j++)
        {
            Vector4 tet = _listOfTetVector[_containedTet[j]];
            Vector4 weights = _weigths[j];

            verts[j] = BaricentricPosition(weights, tet);
        }

        this.GetComponentInChildren<MeshFilter>().mesh.vertices = verts; //Cambiamos la posición del vértice de la mesh a la que indique el array verts
        this.GetComponentInChildren<MeshFilter>().mesh.RecalculateBounds(); //Se recalcula el "bounding volume" de la mesh y todas sus sub-meshes con los datos de sus vértices
        this.GetComponentInChildren<MeshCollider>().sharedMesh = this.GetComponentInChildren<MeshFilter>().mesh; //Se cambia el collider de la bandera para que se vea afectado por los cambios de los vértices

        // Recorremos la lista de muelles para añadir a cada nodo la fuerza
        // elástica de cada muelle. Por la ley de acción y reacción, estas
        // fuerzas son iguales y de sentidos opuestos en los extremos de cada
        // muelle
        foreach (Spring spring in ListOfSprings)
        {
            float stiffness = spring.k * spring.volume / (spring.length0 * spring.length0);

            Vector3 dFactor = spring.u * (spring.length / spring.length0);

            spring.nodeA.force += -stiffness * (spring.length - spring.length0)
                * dFactor;
            spring.nodeB.force += stiffness * (spring.length - spring.length0)
                * dFactor;

            //Frenamos la deformación de los muelles, como la fuerza se divide en nodoA y nodoB, habrá que hacer una pequeña modificación para que sean fuerzas contrarias
            spring.nodeA.force -= dampingSprings * (Vector3.Dot(spring.u, (spring.nodeA.vel - spring.nodeB.vel))) * spring.u; 
            spring.nodeB.force -= dampingSprings * (Vector3.Dot(spring.u, (spring.nodeB.vel - spring.nodeA.vel))) * spring.u;

        }

        // Recorremos de nuevo la lista de nodos para calcular la nueva
        // velocidad, una vez que ya conocemos la fuerza total en cada nodo
        for (int i = 0; i<ListOfNodes.Count; i++)
        {
            Node node = ListOfNodes[i];
            if (!node.fixedNode) // Si el nodo no es fijo
            {
                // v_(n+1) = v_n + h F_n / m
                node.vel += h * node.force / (_masses[i]);
            }
        }
    }

    /// <summary>
    ///  Método de integración de Euler Simpléctico
    /// </summary>
    void integrateSymplecticEuler()
    {
        Vector3 gravity = transform.InverseTransformDirection(g); //Reconvertimos el vector de la gravedad para que la tela caiga hacia el suelo (y no hacia su eje y local)
        // Recorremos la lista de nodos para aplicar las fuerzas a cada uno de
        // ellos
        for (int i = 0; i < ListOfNodes.Count; i++)
        {
            Node node = ListOfNodes[i];

            node.force = -(_masses[i]) * gravity;
            node.force -= (dampingNodes) * node.vel; //Frenamos el movimiento absoluto de los nodos

        }

        // Recorremos la lista de muelles para añadir a cada nodo la fuerza
        // elástica de cada muelle. Por la ley de acción y reacción, estas
        // fuerzas son iguales y de sentidos opuestos en los extremos de cada
        // muelle
        foreach (Spring spring in ListOfSprings)
        {
            float stiffness = spring.k * spring.volume / (spring.length0 * spring.length0);

            Vector3 dFactor = spring.u * (spring.length / spring.length0);

            spring.nodeA.force += -stiffness * (spring.length - spring.length0)
                * dFactor;
            spring.nodeB.force += stiffness * (spring.length - spring.length0)
                * dFactor;


            //Frenamos la deformación de los muelles, como la fuerza se divide en nodoA y nodoB, habrá que hacer una pequeña modificación para que sean fuerzas contrarias
            spring.nodeA.force -= dampingSprings * (Vector3.Dot(spring.u, (spring.nodeA.vel - spring.nodeB.vel))) * spring.u;
            spring.nodeB.force -= dampingSprings * (Vector3.Dot(spring.u, (spring.nodeB.vel - spring.nodeA.vel))) * spring.u;

        }

        // Recorremos de nuevo la lista de nodos para calcular la nueva
        // velocidad y la nueva posición, una vez que ya conocemos la fuerza
        // total en cada nodo
        for (int i = 0; i <ListOfNodes.Count; i++)
        {
            Node node = ListOfNodes[i];
            
            if (!node.fixedNode) // Si el nodo no es fijo
            {
                // v_(n+1) = v_n + h F_n / m
                node.vel += h * node.force / (_masses[i]);
                // r_(n+1) = r_n + h * v_(n+1)
                node.pos += h * node.vel;
                //verts[i] = node.pos; //Asignamos el nuevo valor de la posición del nodo al array verts
            }
        }

        for (int j = 0; j < verts.Length; j++)
        {
            Vector4 tet = _listOfTetVector[_containedTet[j]];
            Vector4 weights = _weigths[j];

            verts[j] = BaricentricPosition(weights, tet);
        }

        this.GetComponentInChildren<MeshFilter>().mesh.vertices = verts; //Cambiamos la posición del vértice de la mesh a la que indique el array verts
        this.GetComponentInChildren<MeshFilter>().mesh.RecalculateBounds(); //Se recalcula el "bounding volume" de la mesh y todas sus sub-meshes con los datos de sus vértices
        this.GetComponentInChildren<MeshCollider>().sharedMesh = this.GetComponentInChildren<MeshFilter>().mesh; //Se cambia el collider de la bandera para que se vea afectado por los cambios de los vértices
    }
}
