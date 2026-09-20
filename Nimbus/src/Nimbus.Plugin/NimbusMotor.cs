using System;
using System.Collections.Generic;
using HarmonyLib;
using Nimbus.Core;
using UnityEngine;
namespace Nimbus;
internal sealed class NimbusMotor:MonoBehaviour
{
    static readonly Action<Ship,float> Controls=AccessTools.MethodDelegate<Action<Ship,float>>(AccessTools.Method(typeof(Ship),"UpdateControlls"));
    static readonly Action<Ship,float> Ashlands=AccessTools.MethodDelegate<Action<Ship,float>>(AccessTools.Method(typeof(Ship),"TakeAshlandsDamage"));
    static readonly Action<Ship,float> Edge=AccessTools.MethodDelegate<Action<Ship,float>>(AccessTools.Method(typeof(Ship),"ApplyEdgeForce"));
    static readonly AccessTools.FieldRef<Ship,Ship.Speed> Throttle=AccessTools.FieldRefAccess<Ship,Ship.Speed>("m_speed");
    Ship ship;Rigidbody body;ZNetView view;Collider solid,deck;float nextPlayers;
    readonly Dictionary<Collider,Player> playerColliders=new Dictionary<Collider,Player>();
    readonly HashSet<Collider> deckIgnored=new HashSet<Collider>();
    readonly HashSet<Collider> ignored=new HashSet<Collider>();
    readonly RaycastHit[] hits=new RaycastHit[64];
    readonly Vector3[] offsets={Vector3.zero,new Vector3(.45f,0,0),new Vector3(-.45f,0,0),new Vector3(0,0,.65f),new Vector3(0,0,-.65f)};
    WaterVolume water;
    Vector3 beforeContact;
    float terrainLoss;
    bool terrainContact,blockedContact;
    void OnCollisionEnter(Collision collision)=>RecordContact(collision);
    void OnCollisionStay(Collision collision)=>RecordContact(collision);
    void RecordContact(Collision collision)
    {
        if(!view || !view.IsValid() || !view.IsOwner())return;
        bool terrain=collision.collider && collision.collider.GetComponentInParent<Heightmap>();
        bool traversable=false;
        for(int i=0;i<collision.contactCount;i++)
            if(collision.GetContact(i).normal.y>HoverRules.MinGroundNormal)traversable=true;
        if(!terrain || !traversable){blockedContact=true;return;}
        terrainContact=true;
        if(beforeContact.sqrMagnitude>.0001f)
            terrainLoss+=Mathf.Max(0,-Vector3.Dot(collision.impulse,beforeContact.normalized)/body.mass);
    }
    readonly float[] waveHeights=new float[5];
    float previousWave=float.NegativeInfinity,waveRise;
    Transform cloudVisual;float nextVegetation,nextTiltSend;float hullClearance=HoverRules.Clearance;float filteredLift=9.81f,climbBlend,filteredClimbRise;Vector3 travelUp=Vector3.up;
    readonly Collider[] vegetationHits=new Collider[256];
    readonly List<Collider> restore=new List<Collider>();
    static bool PassThroughObstacle(Collider collider)
    {
        if(!collider || collider.GetComponentInParent<Character>() || collider.GetComponentInParent<TreeLog>())return false;
        var network=collider.GetComponentInParent<ZNetView>();
        string name=network?Utils.GetPrefabName(network.gameObject):Utils.GetPrefabName(collider.gameObject);
        bool looseStone=name=="Pickable_Stone" || name=="Pickable_Flint";
        return VegetationRules.PassThrough(name,collider.GetComponentInParent<Plant>()!=null,looseStone);
    }
    void IgnoreVegetation()
    {
        if(Time.time<nextVegetation)return;nextVegetation=Time.time+.2f;
        int count=Physics.OverlapSphereNonAlloc(body.position,4,vegetationHits,~0,QueryTriggerInteraction.Ignore);
        var nearby=count==vegetationHits.Length?Physics.OverlapSphere(body.position,4,~0,QueryTriggerInteraction.Ignore):vegetationHits;
        int length=nearby==vegetationHits?count:nearby.Length;
        for(int i=0;i<length;i++)
        {
            var c=nearby[i];if(c && !ignored.Contains(c) && PassThroughObstacle(c)){Physics.IgnoreCollision(solid,c,true);ignored.Add(c);}
        }
        restore.Clear();
        foreach(var c in ignored)if(!c || (c.bounds.center-body.position).sqrMagnitude>100)restore.Add(c);
        foreach(var c in restore){if(c)Physics.IgnoreCollision(solid,c,false);ignored.Remove(c);}
    }
    bool FindClimb(Vector3 intent,int mask,out Vector3 normal,out float height)
    {
        normal=Vector3.up;height=float.NegativeInfinity;
        if(intent.sqrMagnitude<.001f)return false;
        Vector3 direction=intent.normalized;
        // Sample the approaching slope at hull height, not from above a high
        // crest. The closest solid face wins, so walls cannot be seen through.
        for(int sample=0;sample<3;sample++)
        {
            var origin=body.position+Vector3.up*(sample==0?.02f:sample==1?-.12f:.3f);
            int count=Physics.RaycastNonAlloc(origin,direction,hits,1.4f,mask,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;RaycastHit selected=default;
            for(int i=0;i<count;i++)
            {
                var h=hits[i];if(!h.collider || h.collider.transform.IsChildOf(transform) || PassThroughObstacle(h.collider))continue;
                if(h.distance<nearest){nearest=h.distance;selected=h;}
            }
            if(!selected.collider)continue;
            if(selected.collider.GetComponentInParent<TreeBase>())continue;
            if(selected.collider.attachedRigidbody && !selected.collider.GetComponentInParent<TreeLog>())continue;
            float into=Vector3.Dot(direction,selected.normal);
            if(!ClimbRules.CanPower(selected.normal.y,into,nearest))continue;
            normal=selected.normal;height=selected.point.y;return true;
        }
        return false;
    }
    void Tilt(Vector3 worldUp,float dt)
    {
        if(!cloudVisual)return;
        // Align the actual collision shape as well as the presentation. The
        // rigidbody remains upright; contact impulses cannot tumble the rider.
        Vector3 localUp=transform.InverseTransformDirection(worldUp.normalized);
        var target=Quaternion.FromToRotation(Vector3.up,localUp);
        solid.transform.localRotation=Quaternion.RotateTowards(solid.transform.localRotation,target,100*dt);
        float visualLimit=Mathf.Lerp(25,40,Mathf.Clamp01((.7f-worldUp.y)/.6f));
        var visualTarget=Quaternion.RotateTowards(Quaternion.identity,target,visualLimit);
        cloudVisual.localRotation=Quaternion.RotateTowards(cloudVisual.localRotation,visualTarget,55*dt);
        var box=(BoxCollider)solid;var half=box.size*.5f;
        float extent=Mathf.Abs(Vector3.Dot(worldUp,solid.transform.right))*half.x
            +Mathf.Abs(Vector3.Dot(worldUp,solid.transform.up))*half.y
            +Mathf.Abs(Vector3.Dot(worldUp,solid.transform.forward))*half.z;
        float centerOffset=Vector3.Dot(worldUp,solid.transform.TransformVector(box.center));
        hullClearance=MotionSmoothing.Step(hullClearance,HoverRules.HullClearance(worldUp.y,extent,centerOffset),2,1,dt);
        var seat=ship.m_shipControlls.m_attachPoint;
        seat.localRotation=cloudVisual.localRotation;seat.localPosition=cloudVisual.localRotation*(Vector3.up*.28f);
    }
    internal string LastMount="none";
    internal bool Climbing;
    internal int Ticks;internal float Support=float.NegativeInfinity;
    internal string Status()=>"Nimbus ticks="+Ticks+" enabled="+enabled+" owner="+(view && view.IsValid() && view.IsOwner())+" kinematic="+body.isKinematic+" support="+Support+" y="+body.position.y+" rider="+ship.m_shipControlls.GetUser()+" clearance="+hullClearance+" climbing="+Climbing+" mount="+LastMount+" encumbered="+(Player.m_localPlayer && Player.m_localPlayer.IsEncumbered());
    const string InputRpc="Nimbus_MoveInput";
    readonly SprintGate sprintGate=new SprintGate();
    Vector3 localDirection;bool localRun,sprintReceived,lastSprintSent;float localInputTime=float.NegativeInfinity;
    Vector3 moveInput,lastSent;float received=float.NegativeInfinity,nextSend;long inputUser;bool wasOwner;
    void Start()
    {
        if(view && view.IsValid()){view.Register<Vector3,bool>(InputRpc,ReceiveInput);ship.enabled=true;}
    }
    // Own the tick directly rather than depending on the cloned boat's updater registration.
    void FixedUpdate()=>Step(Time.fixedDeltaTime);
    internal void SendInput(Vector3 move,Vector3 look,bool run)
    {
        var player=Player.m_localPlayer;
        if(!player || !view || !view.IsValid() || ship.m_shipControlls.GetUser()!=player.GetPlayerID())return;
        look.y=0;if(look.sqrMagnitude<.0001f)look=transform.forward;look.Normalize();
        localDirection=Vector3.ClampMagnitude(look*move.z+Vector3.Cross(Vector3.up,look)*move.x,1);
        localRun=run;localInputTime=Time.unscaledTime;
    }
    void UpdateLocalInput(float dt)
    {
        var player=Player.m_localPlayer;
        if(!player || player.IsDead() || ship.m_shipControlls.GetUser()!=player.GetPlayerID())
        {sprintGate.Tick(false,false,false,false);localRun=false;return;}
        bool fresh=player.GetControlledShip()==ship && Time.unscaledTime-localInputTime<=.5f;
        Vector3 direction=fresh?localDirection:Vector3.zero;
        bool supported=view.IsOwner()?!float.IsNegativeInfinity(Support):view.GetZDO().GetBool("Nimbus.Supported",false);
        bool sprint=sprintGate.Tick(fresh && localRun,direction.sqrMagnitude>.001f,supported,player.HaveStamina());
        if(sprint)
        {
            // Match vanilla running cost: skill, equipment, status effects and
            // world stamina multipliers. Only the owning player pays once.
            float drain=player.m_runStaminaDrain*Mathf.Lerp(1,.5f,player.GetSkills().GetSkillFactor(Skills.SkillType.Run));
            drain-=drain*player.GetEquipmentMovementModifier();drain+=drain*player.GetEquipmentRunStaminaModifier();
            player.GetSEMan().ModifyRunStaminaDrain(drain,ref drain,direction);
            player.UseStamina(Mathf.Max(0,drain)*dt*Game.m_moveStaminaRate);
            if(!player.HaveStamina()){sprintGate.Tick(true,true,true,false);sprint=false;}
        }
        if(Time.unscaledTime>=nextSend || (direction-lastSent).sqrMagnitude>.0001f || sprint!=lastSprintSent)
        {nextSend=Time.unscaledTime+.1f;lastSent=direction;lastSprintSent=sprint;view.InvokeRPC(InputRpc,direction,sprint);}
    }
    void ReceiveInput(long sender,Vector3 direction,bool sprint)
    {
        if(!view.IsOwner())return;
        long user=ship.m_shipControlls.GetUser();var player=Player.GetPlayer(user);
        if(!player || player.IsDead() || !ship.m_shipControlls.HaveValidUser())return;
        var playerView=player.GetComponent<ZNetView>();
        if(!playerView || !playerView.IsValid() || playerView.GetZDO().GetOwner()!=sender)return;
        if(float.IsNaN(direction.x)||float.IsInfinity(direction.x)||float.IsNaN(direction.z)||float.IsInfinity(direction.z))return;
        direction.y=0;moveInput=Vector3.ClampMagnitude(direction,1);received=Time.unscaledTime;inputUser=user;sprintReceived=sprint && player.HaveStamina();
    }
    void Awake(){cloudVisual=transform.Find("Nimbus white cloud");deck=transform.Find("Nimbus white cloud/Nimbus deck")?.GetComponent<Collider>();ship=GetComponent<Ship>();body=GetComponent<Rigidbody>();view=GetComponent<ZNetView>();solid=ship.m_shipControlls.GetComponent<BoxCollider>();foreach(var c in ship.m_shipControlls.GetComponents<BoxCollider>())if(c.enabled&&!c.isTrigger)solid=c;}
    internal void Step(float dt)
    {
        if(!view || !view.IsValid())return;
        // The cloud's solid hull collides with scenery, not people. Only the
        // attached controller rides it; a second player cannot stand as cargo.
        if(Time.time>=nextPlayers)
        {
            nextPlayers=Time.time+.5f;
            foreach(var player in Player.GetAllPlayers())foreach(var c in player.GetComponentsInChildren<Collider>())
                if(c && !c.isTrigger)
                {
                    playerColliders[c]=player;
                    if(ignored.Add(c))Physics.IgnoreCollision(solid,c,true);
                }
            ignored.RemoveWhere(c=>!c);
        }
        if(deck)
        {
            long user=ship.m_shipControlls.GetUser();
            foreach(var pair in playerColliders)
            {
                var c=pair.Key;var player=pair.Value;if(!c || !player)continue;
                bool ignore=!RiderDeckRules.Supports(user,player.GetPlayerID(),player.GetAttachPoint()==ship.m_shipControlls.m_attachPoint);
                if(ignore && deckIgnored.Add(c))Physics.IgnoreCollision(deck,c,true);
                else if(!ignore && deckIgnored.Remove(c))Physics.IgnoreCollision(deck,c,false);
            }
        }
        IgnoreVegetation();
        UpdateLocalInput(dt);
        Controls(ship,dt);
        if(!view.IsOwner()){wasOwner=false;terrainLoss=0;terrainContact=blockedContact=false;beforeContact=Vector3.zero;Tilt(view.GetZDO().GetVec3("Nimbus.SurfaceUp",Vector3.up),dt);return;}
        if(!wasOwner){received=float.NegativeInfinity;moveInput=Vector3.zero;previousWave=float.NegativeInfinity;waveRise=0;filteredLift=9.81f;climbBlend=0;filteredClimbRise=0;wasOwner=true;}
        bool rider=ship.m_shipControlls.HaveValidUser();
        if(!rider){received=float.NegativeInfinity;moveInput=Vector3.zero;inputUser=0;}
        bool active=rider && HoverRules.FreshInput(Time.unscaledTime,received,ship.m_shipControlls.GetUser(),inputUser);
        var requested=HoverRules.MoveVelocity(moveInput.x,moveInput.z,active);
        Vector3 intent=new Vector3(requested.x,0,requested.z);
        if(active && sprintReceived)intent*=SprintGate.Multiplier;
        // Undo only terrain's opposing horizontal collision impulse. Keep the
        // solver's vertical separation, steering/braking, and solid obstacles.
        Vector3 contactDirection=beforeContact.normalized;
        float restoreSpeed=TerrainMomentum.Restore(beforeContact.magnitude,
            Vector3.Dot(body.linearVelocity,contactDirection),terrainLoss,terrainContact,blockedContact,
            active && Vector3.Dot(intent,contactDirection)>.01f);
        if(restoreSpeed>0)body.linearVelocity+=contactDirection*restoreSpeed;
        terrainLoss=0;terrainContact=blockedContact=false;
        Vector3 surfaceUp=Vector3.up;bool waterHighest=false;float groundSupport=float.NegativeInfinity;int waveCount=0;float waveSum=0;
        float support=float.NegativeInfinity;float waterSurface=float.NegativeInfinity;int mask=LayerMask.GetMask("terrain","piece","Default","static_solid","Default_small");
        Vector3 flatVelocity=new Vector3(body.linearVelocity.x,0,body.linearVelocity.z);
        // Three extra probes look ahead, bounded and requiring existing support so it cannot
        // bridge ravines or grant wall-climbing support.
        for(int sample=0;sample<offsets.Length+3;sample++)
        {
            bool ahead=sample>=offsets.Length;
            if(ahead && float.IsNegativeInfinity(support))break;
            Vector3 point=sample<offsets.Length?transform.TransformPoint(offsets[sample]):body.position+Vector3.ClampMagnitude((intent.sqrMagnitude>flatVelocity.sqrMagnitude?intent:flatVelocity)*(.15f*(sample-offsets.Length+1)),2.25f);
            if(!ahead)
            {
                float waterHeight=Floating.GetWaterLevel(point,ref water);waveHeights[sample]=waterHeight;
                if(HoverRules.WaterSupport(waterHeight,body.position.y))
                {
                    waveCount++;waveSum+=waterHeight;
                    if(waterHeight>support){support=waterHeight;surfaceUp=Vector3.up;waterHighest=true;}
                    waterSurface=Mathf.Max(waterSurface,waterHeight);
                }
            }
            int count=Physics.RaycastNonAlloc(point+Vector3.up*.65f,Vector3.down,hits,4.5f,mask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];if(!hit.collider || hit.collider.transform.IsChildOf(transform) || PassThroughObstacle(hit.collider))continue;
                // Fallen logs are movable rigidbodies, but still provide a top
                // surface to hover over. Other vehicles and creatures do not.
                if(hit.collider.attachedRigidbody && !hit.collider.GetComponentInParent<TreeLog>())continue;
                if(ahead && !HoverRules.CanStep(support,hit.point.y))continue;
                if(HoverRules.CanSupport(hit.point.y,body.position.y,hit.normal.y))
                {
                    groundSupport=Mathf.Max(groundSupport,hit.point.y);
                    if(hit.point.y>support){support=hit.point.y;surfaceUp=hit.normal;waterHighest=false;}
                }
            }
        }
        float surfaceVelocity=HoverRules.SurfaceRise(surfaceUp.x,surfaceUp.y,surfaceUp.z,flatVelocity.x,flatVelocity.z);
        if(waterHighest && waveCount==waveHeights.Length)
        {
            // Fit the footprint's water plane: +X,-X,+Z,-Z samples. Average
            // wave height avoids perching on whichever corner sees the crest.
            float level=waveSum/waveCount;
            if(level>=groundSupport)
            {
                var normal=HoverRules.WaveNormal(waveHeights[2],waveHeights[1],waveHeights[4],waveHeights[3],.9f,1.3f);
                surfaceUp=transform.TransformDirection(new Vector3(normal.x,normal.y,normal.z));support=level;
                float rise=HoverRules.SurfaceVelocity(previousWave,level,dt);
                waveRise=Mathf.Lerp(waveRise,rise,1-Mathf.Exp(-6*dt));surfaceVelocity=waveRise;previousWave=level;
            }
            else{previousWave=float.NegativeInfinity;waveRise=0;}
        }
        else{previousWave=float.NegativeInfinity;waveRise=0;}
        Vector3 climbNormal=Vector3.up;float climbHeight=float.NegativeInfinity;
        Climbing=active && FindClimb(intent,mask,out climbNormal,out climbHeight);
        Vector3 climbMotion=Vector3.zero;
        if(Climbing)
        {
            surfaceUp=climbNormal;support=Mathf.Max(support,climbHeight);waterHighest=false;
            var motion=ClimbRules.Motion(surfaceUp.x,surfaceUp.y,surfaceUp.z,intent.x,intent.z);
            climbMotion=new Vector3(motion.x,motion.y,motion.z);
        }
        Ticks++;Support=support;
        bool supported=!float.IsNegativeInfinity(support);
        if(!supported)filteredLift=9.81f;
        Tilt(surfaceUp,dt);
        travelUp=Vector3.Slerp(travelUp,surfaceUp,1-Mathf.Exp(-6*dt)).normalized;
        climbBlend=MotionSmoothing.Step(climbBlend,Climbing?1:0,4,3,dt);
        filteredClimbRise=MotionSmoothing.Step(filteredClimbRise,Climbing?climbMotion.y:0,6,6,dt);
        if(Time.time>=nextTiltSend){view.GetZDO().Set("Nimbus.SurfaceUp",surfaceUp);view.GetZDO().Set("Nimbus.Supported",supported);nextTiltSend=Time.time+.1f;}
        Throttle(ship)=Ship.Speed.Stop;
        // Water has no solid collider. Catch a descending rider at the water
        // surface instead of allowing a fast fall to tunnel below hover range.
        if(!float.IsNegativeInfinity(waterSurface) && body.position.y<waterSurface+.15f && body.linearVelocity.y< -2)
        {var falling=body.linearVelocity;falling.y=-2;body.linearVelocity=falling;}
        if(supported)
        {
            float hoverLift=HoverRules.Lift(support+(waterHighest?HoverRules.Clearance:hullClearance)-body.position.y,body.linearVelocity.y-surfaceVelocity);
            float lift=Mathf.Lerp(hoverLift,ClimbRules.Lift(filteredClimbRise,body.linearVelocity.y),climbBlend);
            filteredLift=MotionSmoothing.Step(filteredLift,lift,70,70,dt);
            body.AddForce(Vector3.up*filteredLift,ForceMode.Acceleration);
        }
        Vector3 velocity=body.linearVelocity;Vector3 flat=new Vector3(velocity.x,0,velocity.z);
        // Direct camera-relative steering on every surface. Acceleration and
        // braking preserve a little drift; unsupported air gets neither.
        if(supported)
        {
            Vector3 desired=intent;
            if(waterHighest && !Climbing && EnvMan.instance)
            {
                var wind=EnvMan.instance.GetWindDir();
                desired*=TailwindRules.Multiplier(true,active,intent.x,intent.z,wind.x,wind.z,EnvMan.instance.GetWindIntensity());
            }
            // Match the player's ground motion: speed is measured along the
            // slope, not horizontally (which would multiply uphill speed).
            if(Climbing)
            {
                float fraction=ClimbRules.HorizontalFraction(climbMotion.y,body.linearVelocity.y);
                desired=new Vector3(climbMotion.x,0,climbMotion.z)*fraction;
            }
            else if(!waterHighest && desired.sqrMagnitude>.0001f)
            {
                var tangent=Vector3.ProjectOnPlane(desired,travelUp).normalized*desired.magnitude;
                desired=new Vector3(tangent.x,0,tangent.z);
            }
            body.AddForce(Vector3.ClampMagnitude((desired-flat)*4,8),ForceMode.Acceleration);
            if(desired.sqrMagnitude>.01f)body.MoveRotation(Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(desired),120*dt));
        }
        Ashlands(ship,dt);Edge(ship,dt);
        beforeContact=new Vector3(body.linearVelocity.x,0,body.linearVelocity.z);
    }
    void OnDestroy()
    {
        if(solid)foreach(var c in ignored)if(c)Physics.IgnoreCollision(solid,c,false);
        if(deck)foreach(var c in deckIgnored)if(c)Physics.IgnoreCollision(deck,c,false);
    }
}
