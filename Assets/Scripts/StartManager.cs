using UnityEngine;
using Photon.Pun;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class StartManager:MonoBehaviourPunCallbacks
{
    [SerializeField]
    Text loading;
    [SerializeField]
    Animator animator;
    public void StartClick()
    {
        PhotonNetwork.ConnectUsingSettings();
        Console.WriteLine("Start");
        loading.text = "Loading...";
    }
    public override void OnConnectedToMaster()
    {
        animator.SetTrigger("Start");
        print("Connecting");

        StartCoroutine(WaitStartAnimationThenLoadScene());
    }

    private IEnumerator WaitStartAnimationThenLoadScene()
    {
        yield return null;

        
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        yield return new WaitForSeconds(stateInfo.length);

        SceneManager.LoadScene("LobbyScene");
    }
}
