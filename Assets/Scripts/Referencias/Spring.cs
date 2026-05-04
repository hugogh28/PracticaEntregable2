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
* Breve descripción: La siguiente clase de C# gestiona todos los cálculos que deben efectuarse en la creación de un muelle (y lo prepara para sus posteriores modificaciones en MassSpringCloth), de este modo, se logra que 
* el animador pueda ver por pantalla una físicas adecuadas.
*****************************************************************************/

namespace Practica1
{
    public class Spring
    {
        public float k = 100f; // Constante de rigidez del muelle (N/m)
        public float length0; // Longitud natural del muelle (ahí la fuerza elástica
                              // se anula)
        public float length; // Longitud del muelle en un momento dado
        public Vector3 pos; // Posición 3D del punto medio del muelle
        public Vector3 u; // Vector unitario con la dirección del muelle que
                          // apunta de B a A
        public float defaultSize = 2f; // Longitud natural de los cilindros en
                                       // Unity (m)
        public Quaternion rotation; // Nos permitirá calcular la orientación del
                                    // muelle  
        public Node nodeA; // Primer extremo del muelle
        public Node nodeB; // Segundo extremo del muelle

        public Spring(float cElasticity, Node A, Node B)
        {
            k = cElasticity; //Asignamos al nodo su constante de rigidez
            nodeA = A; //El nodo A del muelle, corresponde con el primer nodo recibido al crear un nuevo muelle
            nodeB = B; //El nodo B del muelle, corresponde con el segundo nodo recibido al crear un nuevo muelle
            u = VectorBetweenNodes(A, B); //Vector dirección entre el primer y el segundo nodo del muelle
            length0 = u.magnitude; //Longitud inicial del muelle, calculada en base al vector de dirección inicial entre sus dos nodos
            u = Vector3.Normalize(u); //Vector normalizado que almacena la orientación del muelle
            pos = (A.pos + B.pos) / 2f; //Posición del punto medio del muelle, calculado en base a la media aritmética de las posiciones de sus nodos
            rotation = Quaternion.FromToRotation(Vector3.up, u); //Orientación del muelle según el vector dirección

            length = length0; //Asignamos el cálculo inicial de la longitud al muelle (para poder hacer cálculos a posteriori)
        }

        Vector3 VectorBetweenNodes(Node A, Node B) //Calcula el vector (de dirección) entre dos nodos 
        {
            return new Vector3(B.pos.x - A.pos.x, B.pos.y - A.pos.y, B.pos.z - A.pos.z);
        }
    }
}
