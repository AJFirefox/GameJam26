using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
   
    public void PlayGame()
    {
        SceneManager.LoadScene("AustinBCooking");
    }


    public void QuitGame()
    {
        
    }

    public void ReplayGame() 
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    }
}
