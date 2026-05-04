using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/******************************************************************************
* GRADO EN DISEÑO Y DESARROLLO DE VIDEOJUEGOS - ANIMACIÓN 3D
* Bloque 2 - Práctica Entregable 1
*
* Nombre y apellidos: Hugo García Hernández
* DNI: 03212391G
* Curso académico: 2025-2026
*
* Nombre de la clase: MassSpringCloth
* Breve descripción: La siguiente clase de C# gestiona todos los cálculos que debene efectuarse en el objeto masa-muelle, para así obtener unas físicas adecuadas.
* Esto se logra desde la asignación de nodos y muelles al mallado del objeto (plano/tela), hasta la aplicación de los métodos de integración Euler explícito, el cual, debido a su 
* inestabilidad, no es recomendable usarlo, y Euler simpléctico.
*****************************************************************************/
namespace Practica1
{
    public class MassSpringCloth : MonoBehaviour
    {
        [Header("Modificadores de la animación")]
        public bool paused; //Booleano que nos servirá para pausar la animación
        public float mass; //Masa del objeto (100 gramos)
        public Vector3 g; //El valor de la gravedad aplicado al objeto masa-muelle (está en m/s)
        public float dampingNodes; //Amortiguamiento para el movimiento absoluto de los nodos
        public float dampingSprings; //Amortiguamiento para frenar la deformación de los muelles

        public enum Integration //Los diferentes métodos de integración disponibles
        {
            ExplicitEuler = 0,
            SymplecticEuler = 1
        }

        [Header("Métodos de integración")]
        public Integration integrationMethod; //Este será el método de integración escogido

        [Header("Paso de integración")]
        public float h; //El paso de integración (cuanto más rápido sea, más inestable puede ser)

        public List<Spring> ListOfSprings; //Lista de muelles
        bool springListIsFull = false; //Booleano para comprobar si la lista de muelles está llena
        bool nodeListIsFull = false; //Booleano para comprobar si la lista de nodos está llena

        public List<Node> ListOfNodes; //Lista de nodos


        [Header("Constantes de rigidez")]
        public float kT; //Constante de rigidez de los muelles de tracción
        public float kF; //Constante de rigidez de los muelles de flexión

        [Header("Fijadores")]
        public List<Fixer> fixer = new List<Fixer>(); //Desde Unity se hará por esta línea la asignación del fixer, es decir, del cubo que fija nodos, a este script para que los nodos se fijen

        Mesh cloth;

        Vector3[] verts;

        Vector3Int[] edges; //Creamos un array edges de tipo Vector3Int (para ahorrarnos el casting de float a int), para asignar las aristas 

        void Start()
        {
            Mesh mesh = this.GetComponent<MeshFilter>().mesh; //Se guarda en la variable mesh el mallado del objeto

            cloth = mesh; //Para poder hacer las modificaciones en la malla, se guarda la mesh en una variable global

            Vector3[] vertices = mesh.vertices; //Se guardan en un array todos los vértices de la mesh

            verts = vertices; //Para poder hacer las modificaciones en la mesh, se guardan los vértices de la mesh en una variable global

            List<Node> nodes = new List<Node>(vertices.Length); //Se crea una lista de nodos cuyo tamaño sea el de los vértices de la mesh
            List<Spring> springs = new List<Spring>(); //Se crea una lista de muelles cuyo tamaño es indefinido (ya que se presupone que podemos usar cualquier bandera)
            List<Spring> springsF = new List<Spring>(); //Se crea una lista de muelles cuyo tamaño es indefinido (ya que se presupone que podemos usar cualquier bandera)

            int[] triangles = mesh.triangles; //Se guardan en un array todos los triángulos de la mesh

            for (int i = 0; i < vertices.Length; i++) //Se itera tantas veces como vértices hay en el array vertices, complejidad O(n)
            {
                nodes.Add(new Node(vertices[i], fixer, transform)); //Cada vez que se itera sobre el bucle de vértices de la mesh, se añade un nuevo nodo, cuya posición corresponde a la de su vértice
                                                                    //Además, se comprueba, mediante la lista de fixers, si dicho nodo debe estar fijado antes de comenzar la animación y, para dicha comprobació,
                                                                    //es necesario reconvertir de coordenadas locales a globales, por lo que pasamos el componente transform del objeto masa-muelle al constructor del nodo

                verts[i] = nodes[i].pos; //Se rellena el array verts con sus correspondientes nodos del array nodes
            }
            nodeListIsFull = true; //Se activa el booleano nodeListIsFull cuando la lista de nodos se ha llenado con todos los elementos del objeto

            ListOfNodes = nodes; //Para poder hacer uso de OnDrawGizmos() se pasa la lista nodes a ListOfNodes

            edges = new Vector3Int[triangles.Length]; //Creamos la estructura edges, para almacenar todas las aristas

            for (int i = 0; i < edges.Length - 1; i += 3) //Recorremos el array triangles, para asignar a edges cada arista
            {
                edges[i] = new Vector3Int(Math.Min(triangles[i], triangles[i + 1]), Math.Max(triangles[i], triangles[i + 1]), triangles[i + 2]); // ABC
                edges[i + 1] = new Vector3Int(Math.Min(triangles[i], triangles[i + 2]), Math.Max(triangles[i], triangles[i + 2]), triangles[i + 1]); // ACB
                edges[i + 2] = new Vector3Int(Math.Min(triangles[i + 1], triangles[i + 2]), Math.Max(triangles[i + 1], triangles[i + 2]), triangles[i]);// BCA 
            }

            edges = edges.OrderBy(edge => edge.x).ThenBy(edge => edge.y).ToArray(); //Ordenamos el array edges en función del primer parámetro de una arista, y luego, en función del segundo parámetro

            for (int i = 0; i < edges.Length; i++) //Se itera tantas veces como aristas hay (600 en el caso original)
            {

                if (i < edges.Length - 1 && edges[i].x == edges[i + 1].x && edges[i].y == edges[i + 1].y) //Si dos aristas (adyacentes en la lista) se detectan como duplicadas, se añadirá un nodo de flexión y se evitará añadir un muelle de tracción
                {
                    springs.Add(new Spring(kT, nodes[edges[i].x], nodes[edges[i].y])); //Se añade un nodo de tracción en la arista compartida entre nodos opuestos de triángulos adyacentes

                    springs.Add(new Spring(kF, nodes[edges[i].z], nodes[edges[i + 1].z])); //Se añade un nodo de flexión entre nodos opuestos de triángulos adyacentes

                    i++; //Saltamos una posición para evitar duplicar muelles
                }
                else
                {
                    springs.Add(new Spring(kT, nodes[edges[i].x], nodes[edges[i].y])); //Añade un muelle de tracción entre los vértices de la arista
                }
            }

            springListIsFull = true; //Se activa el booleano springListIsFull cuando la lista de muelles se ha llenado con todos los elementos del objeto

            ListOfSprings = springs; //Para poder hacer uso de OnDrawGizmos() se pasa la lista springs a ListOfSprings
        }

        private void OnDrawGizmos()
        {
            //Dibujado de los gizmos de los nodos en coordenadas globales
            Gizmos.matrix = transform.localToWorldMatrix; //Se hace el paso de coordenadas locales a globales para evitgar que los gizmos se pinten en otro lugar que no sea el nodo correspondiente
            DrawIfNotNull(); //Se trata de dibujar los gizmos
        }

        //Para evitar llamadas a elementos no existentes usamos el método DrawIfNotNull, que comprueba si las listas de nodos y muelles han sido inizialidas y llenadas para evitar
        //rellenar Gizmos que no existen
        void DrawIfNotNull()
        {
            if (nodeListIsFull)
            {
                Gizmos.color = Color.green; //Se asigna color verde a los gizmos esféricos de los nodos
                foreach (var node in ListOfNodes) //Se recorre cada nodo de la lista
                {
                    Gizmos.DrawSphere(node.pos, 0.2f); //Se pinta una esfera de radio 0.2 (unidades de Unity) sobre cada nodo de la lista
                }
            }

            if (springListIsFull)
            {
                Gizmos.color = Color.red;
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
                }
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
            int i = 0;
            Vector3 gravity = transform.InverseTransformDirection(g); //Reconvertimos el vector de la gravedad para que la tela caiga hacia el suelo (y no hacia su eje y local)
                                                                      // Recorremos la lista de nodos para aplicar las fuerzas a cada uno de
                                                                      // ellos
            foreach (Node node in ListOfNodes)
            {
                if (!node.fixedNode) // Si el nodo no es fijo
                {
                    // r_(n+1) = r_n + h * v_n

                    node.pos += h * node.vel;
                    verts[i] = node.pos; //Asignamos el nuevo valor de la posición del nodo al array verts
                    node.force = -(mass) * gravity;


                    node.force -= (dampingNodes) * node.vel; //Frenamos el movimiento absoluto de los nodos

                }
                i++; //Como se ha terminado una iteración, sumamos 1 sobre la variable que hemos creado para permitir que verts[i] y node, correspondan en posición
            }
            this.GetComponent<MeshFilter>().mesh.vertices = verts; //Cambiamos la posición del vértice de la mesh a la que indique el array verts
            this.GetComponent<MeshFilter>().mesh.RecalculateBounds(); //Se recalcula el "bounding volume" de la mesh y todas sus sub-meshes con los datos de sus vértices
            this.GetComponent<MeshCollider>().sharedMesh = this.GetComponent<MeshFilter>().mesh; //Se cambia el collider de la bandera para que se vea afectado por los cambios de los vértices

            // Recorremos la lista de muelles para añadir a cada nodo la fuerza
            // elástica de cada muelle. Por la ley de acción y reacción, estas
            // fuerzas son iguales y de sentidos opuestos en los extremos de cada
            // muelle
            foreach (Spring spring in ListOfSprings)
            {
                spring.nodeA.force += -spring.k * (spring.length - spring.length0)
                    * spring.u;
                spring.nodeB.force += spring.k * (spring.length - spring.length0)
                    * spring.u;

                //Frenamos la deformación de los muelles, como la fuerza se divide en nodoA y nodoB, habrá que hacer una pequeña modificación para que sean fuerzas contrarias
                spring.nodeA.force -= dampingSprings * (Vector3.Dot(spring.u, (spring.nodeA.vel - spring.nodeB.vel))) * spring.u;
                spring.nodeB.force -= dampingSprings * (Vector3.Dot(spring.u, (spring.nodeB.vel - spring.nodeA.vel))) * spring.u;

            }

            // Recorremos de nuevo la lista de nodos para calcular la nueva
            // velocidad, una vez que ya conocemos la fuerza total en cada nodo
            foreach (Node node in ListOfNodes)
            {
                if (!node.fixedNode) // Si el nodo no es fijo
                {
                    // v_(n+1) = v_n + h F_n / m
                    node.vel += h * node.force / (mass);
                }
            }
        }

        /// <summary>
        ///  Método de integración de Euler Simpléctico
        /// </summary>
        void integrateSymplecticEuler()
        {
            int i = 0;
            Vector3 gravity = transform.InverseTransformDirection(g); //Reconvertimos el vector de la gravedad para que la tela caiga hacia el suelo (y no hacia su eje y local)
                                                                      // Recorremos la lista de nodos para aplicar las fuerzas a cada uno de
                                                                      // ellos
            foreach (Node node in ListOfNodes)
            {
                node.force = -(mass) * gravity;


                node.force -= (dampingNodes) * node.vel; //Frenamos el movimiento absoluto de los nodos

            }

            // Recorremos la lista de muelles para añadir a cada nodo la fuerza
            // elástica de cada muelle. Por la ley de acción y reacción, estas
            // fuerzas son iguales y de sentidos opuestos en los extremos de cada
            // muelle
            foreach (Spring spring in ListOfSprings)
            {
                spring.nodeA.force += -spring.k * (spring.length - spring.length0)
                    * spring.u;
                spring.nodeB.force += spring.k * (spring.length - spring.length0)
                    * spring.u;


                //Frenamos la deformación de los muelles, como la fuerza se divide en nodoA y nodoB, habrá que hacer una pequeña modificación para que sean fuerzas contrarias
                spring.nodeA.force -= dampingSprings * (Vector3.Dot(spring.u, (spring.nodeA.vel - spring.nodeB.vel))) * spring.u;
                spring.nodeB.force -= dampingSprings * (Vector3.Dot(spring.u, (spring.nodeB.vel - spring.nodeA.vel))) * spring.u;

            }

            // Recorremos de nuevo la lista de nodos para calcular la nueva
            // velocidad y la nueva posición, una vez que ya conocemos la fuerza
            // total en cada nodo
            foreach (Node node in ListOfNodes)
            {

                if (!node.fixedNode) // Si el nodo no es fijo
                {
                    // v_(n+1) = v_n + h F_n / m
                    node.vel += h * node.force / (mass);
                    // r_(n+1) = r_n + h * v_(n+1)
                    node.pos += h * node.vel;
                    verts[i] = node.pos; //Asignamos el nuevo valor de la posición del nodo al array verts
                }
                i++; //Como se ha terminado una iteración, sumamos 1 sobre la variable que hemos creado para permitir que verts[i] y node, correspondan en posición
            }
            this.GetComponent<MeshFilter>().mesh.vertices = verts; //Cambiamos la posición del vértice de la mesh a la que indique el array verts
            this.GetComponent<MeshFilter>().mesh.RecalculateBounds(); //Se recalcula el "bounding volume" de la mesh y todas sus sub-meshes con los datos de sus vértices
            this.GetComponent<MeshCollider>().sharedMesh = this.GetComponent<MeshFilter>().mesh; //Se cambia el collider de la bandera para que se vea afectado por los cambios de los vértices
        }
    }
}
