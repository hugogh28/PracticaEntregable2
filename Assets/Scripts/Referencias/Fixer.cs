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
* Breve descripción: La siguiente clase de C# gestiona cómo debe comportarse un objeto "fixer" en la escena, de modo que el animador pueda mover dicho objeto como considere
* conveniente y pueda, de este modo, fijar tantos nodos del objeto masa-muelle como quiera.
*****************************************************************************/
namespace Practica1
{
    public class Fixer : MonoBehaviour
    {
        public Vector3 pos; //Posición que tendrá el fixer en el mundo
        public bool hidden; //Variable booleano que marcará si se oculta el fixer (true) o no (false)

        void Start()
        {
            pos = transform.position; //Se toma la posición del "fixer"
            this.GetComponent<MeshRenderer>().enabled = true; //Se muestra el "fixer" en primera instancia, para ocultarlo se podrá pulsar la tecla 'H' o presionar sobre el tick del panel
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyUp(KeyCode.S))
            { //Al pulsar la tecla S se podrá ocultar o mostrar todos los "fixers"
                hidden = !hidden;
                Hide();
            }
            Hide(); //Esto permitirá manejar que se muestre (u oculte) solo un "fixer", usando el tick de su interfaz
                    //Esta función puede desincronizar el MeshRenderer de los "fixers" (uno puede estar oculto y el otro no),
                    //aunque esto, se soluciona simplemente volviendo a cambiar el valor de hidden en el inspector de Unity

        }

        void Hide() //Mediante este método, se gestiona el ocultamiento del fixer
        {
            if (hidden) //Si se le ha indicado que debe ocultarse, se ocultará
                gameObject.GetComponent<MeshRenderer>().enabled = false;
            else //Si se le ha indicado que debe mostrarse, se mostrará
                gameObject.GetComponent<MeshRenderer>().enabled = true;
        }
    }
}
