using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioOcclusion : MonoBehaviour
{

   public float fadeSpeed = 1;
   private AudioSource audio; // your audio source
   private bool muted; //is it muted? (yes or no)

   private void Start()
   {
       audio = GetComponent<AudioSource>();
   }
   private void OnTriggerEnter(Collider other) //something collided with us
   {
      if(other.tag == "Player") //if its tag is Player then
      {
		muted = true; //mute should be yes
      }
   }

   private void OnTriggerExit(Collider other) //something left the trigger
   {
      if(other.tag == "Player") //was it a player
      {
		muted = false; // mute should be no
      }
   }

    private void Update() //this goes every single frame of your game
   {
      if(muted) //if its muted at any point
      {
		 audio.volume += fadeSpeed * Time.deltaTime; //out audio component?s volume value should decrease by 1 every second
         //(time.deltatime decreases 1 every second by default)
      }
      else if(!muted) //else can mean "otherwise", so otherwise if its not muted then...
      {
         audio.volume -= fadeSpeed * Time.deltaTime; //add to the volume instead of decreasing
      }
   }
} 