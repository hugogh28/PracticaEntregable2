using System.Collections.Generic;
using System.Collections;
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
* Breve descripción: La siguiente clase de C# gestiona si un nodo debe estar fijo o no, en el momento de su creación y, además, también prepara al objeto Node para que,
* en caso de ser modificada la fuerza, posición (u otra variable) de un nodo en MassSpringCloth, se puedan acceder a dichos componentes.
*****************************************************************************/

namespace Practica1
{
    public class Node
    {
        //public float mass = 5f;
        public bool fixedNode;

        public Vector3 pos;
        public Vector3 posGlobal;
        public Vector3 vel;
        public Vector3 force;
        public List<Vector3> offset = new List<Vector3>();
        List<float> sqrDistance = new List<float>();

        public Node(Vector3 assignedPos, List<Fixer> fixers, Transform massSpringObject) //Constructor de la clase, para asignar los distintos nodos a su correspondiente vértice
        {
            pos = assignedPos; //Tomamos la posición del vértice en coordenadas lcoales
            posGlobal = massSpringObject.TransformPoint(pos); //Reconvertimos la posición del vértice a coordenadas globales
            Collider fixerCollider; //Creamos una variable de tipo Collider para comprobar si el nodo está dentro del Collider del "fixer"
            for (int i = 0; i < fixers.Count; i++)
            {
                fixerCollider = fixers[i].GetComponent<Collider>(); //Asignamos a fixerCollider un valor distinto, uno por cada iteración sobre la lista de "fixers"

                if (fixerCollider.bounds.Contains(posGlobal)) //Si el nodo (en coordenadas globales) se encuentra dentro del "fixer", se marca como fijo
                {
                    fixedNode = true;
                }
                else if (fixedNode == false) //Para evitar que en la segunda iteración, se registre a nodos fijos como no fijos
                {
                    fixedNode = false;
                }
            }
        }
    }
}
