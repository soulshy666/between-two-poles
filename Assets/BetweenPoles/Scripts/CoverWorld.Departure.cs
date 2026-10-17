using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class CoverWorld {
 public bool PickupInProgress {get;private set;}
 public const float PickupDuration=4.1f;
 Vector3 pickupActorPosition,pickupActorScale,pickupShipScale,dock;
 Quaternion pickupActorRotation;
 public IEnumerator CollectAndDepart(){
  if(!actor||!ship)yield break;
  BeginPickup();
  for(float t=0;t<PickupDuration;t+=Time.unscaledDeltaTime){SamplePickup(t);yield return null;}
  SamplePickup(PickupDuration);
 }
 public void BeginPickup(){
  if(PickupInProgress||!actor||!ship)return;
  PickupInProgress=true;
  pickupActorPosition=actor.localPosition;pickupActorRotation=actor.localRotation;
  pickupActorScale=actor.localScale;pickupShipScale=ship.localScale;
  dock=pickupActorPosition+new Vector3(1.55f,.55f,-.6f);
  ship.gameObject.SetActive(true);SamplePickup(0);
 }
 static float PickupEase(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
 static Vector3 PickupPixel(Vector3 p){const float pixel=10f/384;return new Vector3(Mathf.Round(p.x/pixel)*pixel,Mathf.Round(p.y/pixel)*pixel,p.z);}
 public void SamplePickup(float elapsed){
  if(!PickupInProgress||!ship||!actor)return;
  // Travel right to left at the docking height, with the model's +Z nose facing left.
  float approach=PickupEase(elapsed/1.9f);
  ship.localPosition=PickupPixel(Vector3.Lerp(new Vector3(14,dock.y,dock.z),dock,approach));
  ship.localScale=Vector3.one*1.5f;
  ship.localRotation=Quaternion.LookRotation(Vector3.left,new Vector3(0,.7f,-.7f));
  float board=PickupEase((elapsed-1.9f)/.95f);
  float held=Mathf.Floor(board*12)/12;
  var cabin=dock+new Vector3(0,0,.8f);
  actor.localPosition=PickupPixel(Vector3.Lerp(pickupActorPosition,cabin,board));
  actor.localRotation=Quaternion.Slerp(pickupActorRotation,Quaternion.Euler(12,170,-35),held);
  actor.localScale=pickupActorScale*Mathf.Lerp(1,.22f,held);
  actor.gameObject.SetActive(elapsed<2.85f);
  // Once aboard, continue left without turning. The menu owns the final black transition.
  float depart=Mathf.Clamp01((elapsed-2.85f)/1.25f);
  if(depart>0)ship.localPosition=PickupPixel(Vector3.Lerp(dock,new Vector3(-16,dock.y,dock.z),depart*depart));
 }
 void ResetPickup(){
  if(PickupInProgress){
   if(actor){actor.gameObject.SetActive(true);actor.localScale=pickupActorScale;actor.localPosition=pickupActorPosition;actor.localRotation=pickupActorRotation;}
   if(ship)ship.localScale=pickupShipScale;
  }
  PickupInProgress=false;if(ship)ship.gameObject.SetActive(false);
 }
}
}
