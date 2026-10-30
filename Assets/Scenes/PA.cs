using System;
using System.Collections.Generic; 
using Unity.Mathematics;
using UnityEngine; 

public class PA : MonoBehaviour
{
    //三明治先生20261030
    //三明治先生20261030
    //三明治先生20261030
    [Header("本脚本不提供输入移动方案 需要外部向量")]
    [Tooltip("此参数应该被每帧赋值")]public Vector3 movementDirection; 
    [Header("具体设置")]
    public LayerMask GroundMask;
    [Tooltip("射线向下投射长度")] public float rayDistance = 10;
    [Tooltip("触发迈步阈值")] public float updataStepDistance = 3;
    [Tooltip("建议关闭刚体重力")] public float exGravity = 0;
    [Tooltip("双脚过近阈值")] public float tooCloseDistance = 0.5f;
    public Rigidbody rigidbody;
    [Header("骨骼")]
    public Transform root;
    public Transform hip;
    public Transform chest; 
    public float speed = 2;
    [Tooltip("骨盆起伏幅度")] public float hipBob = 0.1f;
    [Header("躯干倾角后退比例")] public float backLeanScale = 0.8f;
    [Header("idle补偿倾角")] public float idleLeanOffset = 0f;
    public bool allowArms = true;
    private Quaternion rootBaseLocal;
    private Quaternion chestBaseLocal;
    [Header("点积方向移动减弱")]
    [Tooltip("1为仅正向笔直奔跑时不减弱 -1为完全不减弱")] public float dotThreshold = 0.6f;  
    public enum WitchLeg { L, R }
    public WitchLeg leg; 
    private Vector3 posPre;
    private Vector3 hipBaseLocal;         // hip 未偏移时的局部位置 
    public int currentMovementIndex;

    [Serializable]
    public class Movement
    {
        public string moveMentName;
        [Range(0.1f, 20)] public float expectTime = 1;
        public KeyCode[] buttons;
        public bool isIdle;
        [Header("迈步姿态")]
        [Tooltip("抬腿曲线最高高度")] public float peak = 0.4f;
        [Tooltip("抬腿曲线上升速度")] public float risePow = 1.5f;
        [Tooltip("抬腿曲线下降速度")] public float fallPow = 1.6f;
        [Header("摆臂姿态")]
        [Tooltip("摆臂曲线位置")] public Vector3 arcPos = new(0,-2,0);
        [Tooltip("摆臂曲线旋转")] public Vector3 arcEuler;
        [Tooltip("摆臂角度")] public float arcSweep= 30f;
        [Tooltip("摆臂速度")] public float armSwingSpeed = 2f;
        [Tooltip("曲线长度")] public float arcLength = 2f;     //  中间到一端的前后距离 
        [Tooltip("曲线弯曲度")] public float arcBow = 0.1f;     //  中间与两端的高度差 
        [Header("身体姿态")]
        [Tooltip("躯干倾斜度")] public float bodyLean = 10;
        [Tooltip("躯干倾斜速度")] public float bodyLeanSpeed = 2;
        [Tooltip("胸腔倾斜度")] public float chestLean = 0;
        [Tooltip("胸腔倾斜速度")] public float chestLeanSpeed = 2;
    }
    [Tooltip("索引越大优先级越大 走路应该是0")] public List<Movement> movements = new List<Movement>();
    [Serializable]
    public class LegPA
    {
        public Transform Thigh;
        public Transform Tar;
        [Tooltip("调试用 非必填")]public Transform goTo;
        [HideInInspector] public Vector3 destinPre;
        [HideInInspector] public Vector3 currentPos;
        [HideInInspector] public Vector3 destinPoint;
        [HideInInspector] public float destinFlushTime;
        [HideInInspector] public bool startedTurnToStanding;
        [HideInInspector] public float moveTimePre = 1.1f;
    }
    [Serializable]
    public class ArmPA
    {
        public Transform UpperArm;
        public Transform LowerArm;
        public Transform Hand; 
        public Transform Tar;
        //public Vector3 destinPre;
        //[HideInInspector] public Vector3 currentPos;
        // public Vector3 destinPoint;
        //[HideInInspector] public float destinFlushTime;
        //[HideInInspector] public bool startedTurnToStanding;
        //[HideInInspector] public float moveTimePre = 1.1f;
    }
    public LegPA LLeg, RLeg; 
    public ArmPA LArm, RArm; 
    public enum LegStage { Stop, Moving }

    void Awake()
    {
        if (hip != null) hipBaseLocal = hip.localPosition;
        if(root!=null) rootBaseLocal = root.localRotation;
        if(chest!=null) chestBaseLocal = chest.localRotation;
    }

    void LateUpdate()
    { 
        Movement currentMovement = null;
        for (int i = 0; i < movements.Count; i++)
        {
            if (movements[i].isIdle)
            {
                currentMovement = movements[i];
                currentMovementIndex = i;
                break;
            }
        }
        for (int i = 0; i < movements.Count; i++)
        {
            for (int j = 0; j < movements[i].buttons.Length; j++)
            {
                if (Input.GetKey(movements[i].buttons[j]))
                {
                    currentMovement = movements[i];
                    currentMovementIndex = i;
                }
            }
        }
        if (currentMovement == null)
        {
            Debug.LogError("你未设置Idle姿态 检查bool");
            return;
        } 
        bool bodyMoving = (root.position - posPre).magnitude > 0.02f;
        //
        //Vector3 cameraView = transform.position - Camera.instance.mainCamera.transform.position;
        //cameraView.y = 0;
        //transform.forward = (cameraView);
        //
        //Vector3 bodyForward = (Camera.instance.bodyForward.position - Camera.instance.transform.position);//身体运动方向
        bool standing = movementDirection.magnitude < 0.1f;//动/不动
        if (bodyMoving && standing)
        {
            standing = false;
            movementDirection = (root.position - posPre);
        }
        movementDirection = standing ? Vector3.zero : movementDirection.normalized;
        if (leg == WitchLeg.L)
        {
            LLeg.Tar.position = MoveLeg(LLeg, currentMovement, movementDirection, standing);//计算迈出脚位置
            RLeg.Tar.position = RLeg.currentPos;//脚target位于局部空间 不动也必须更新 
        }
        else
        {
            LLeg.Tar.position = LLeg.currentPos;
            RLeg.Tar.position = MoveLeg(RLeg, currentMovement, movementDirection, standing);
        }
        posPre = root.position;
        float bodyDot = Vector3.Dot(movementDirection, hip.forward); 
        BodyRotation( currentMovement, bodyDot,  standing);
    }
    private Vector3 MoveLeg(LegPA pa, Movement movement, Vector3 bodyForward, bool standFlag)
    {
        float expectTime = movement.expectTime/speed;//步伐必须完成的时间
        if (pa.moveTimePre  != expectTime)//腿部状态有变 要防止跳变
        {
            expectTime = pa.moveTimePre ;
        }
        else
        {
            pa.moveTimePre = movement.expectTime / speed;
        }
        //expectTime /= speed;
        float t = (Time.time - pa.destinFlushTime) /  expectTime;//步伐当前进度
        t = Mathf.Clamp01(t);
        Vector3 lerpPos = Vector3.Lerp(pa.destinPre, pa.destinPoint, t);
        LegPA anotherLeg = leg == WitchLeg.L ? RLeg : LLeg;
        float bodyDot = Vector3.Dot(bodyForward, hip.forward);
        bool isBackOff = bodyDot <= 0;
        if (0 < t && t <= 0.4f)
        {
            if (standFlag)//收脚
            {
                if (pa.startedTurnToStanding)
                { 
                }
                else
                {
                    UpdateDestinationForce(pa, true);//仅仅重算落点 t继续增加 导致跳变 不能用t或帧时差
                    pa.startedTurnToStanding = true;
                    pa.destinFlushTime = Time.time;
                    pa.destinPre = pa.currentPos;
                }
                lerpPos = Vector3.Lerp(pa.destinPre, pa.destinPoint, t);//神奇数字
            }
        }
        //else if (0.1f < t && t <= 0.4f)//正常摆动
        //{

        //}
        //else if (0.4f < t && t <= 0.6f)//盆骨峰值
        //{

        //}
        //else if (0.6f < t && t <= 0.9f)
        //{

        //}
        //else if (0.9f < t && t < 1f)//即将换腿
        //{
        //}
        else if (t >= 1)//脚已落地
        {
            pa.startedTurnToStanding = false;
            if (standFlag)
            {
                UpdateDestinationForce(anotherLeg);//腿根部直接射线新落点
            }
            else
            { 
                UpdateDestination(anotherLeg, bodyDot,(bodyForward /  expectTime) * speed, anotherLeg.currentPos);//新落点 受身体方向和完成时间控制
                //LArm.destinPre = LArm.destinPoint;
            }
            //判断下一次应该摆哪条腿
            float cur = GetLegOffBalance(pa);
            float another = GetLegOffBalance(anotherLeg);
            LegPA nextLeg = (cur > another) ? pa : anotherLeg;
            leg = nextLeg == RLeg ? WitchLeg.R : WitchLeg.L;
            anotherLeg.moveTimePre = nextLeg.moveTimePre = movement.expectTime/speed;//正式接受变化 
            //arm 
        }

        HipFlow(t,standFlag);
        ChestRotation(movement, t, isBackOff, standFlag, pa == LLeg);
        //正确的迈步长度应该是bodyForward *( pa.destinPoint - footMid.position).magnitude
        //但是效果不佳 奇怪
        Vector3 legStep = standFlag ? Vector3.zero : bodyForward + (pa.destinPoint - root.position);//迈步长度
        Vector3 force = (speed * t * legStep / expectTime);
        //Rigidbody body = Camera.instance.target.attachedRigidbody;
        if (float.IsNaN(force.x) || float.IsInfinity(force.x)) force.x = 0f;
        if (float.IsNaN(force.y) || float.IsInfinity(force.y)) force.y = 0f;
        if (float.IsNaN(force.z) || float.IsInfinity(force.z)) force.z = 0f;
        force = Vector3.Lerp(rigidbody.velocity, force, 10 * Time.deltaTime);
        force.y += exGravity; 
        rigidbody.velocity = force;
        pa.currentPos = lerpPos;
        bool stood = (pa.destinPre - pa.destinPoint).magnitude < 0.1f;
        //arm 
        //站立不动时出现的LR切换会导致手臂抽搐 统一为L
        bool isL = pa == LLeg;
        if (!stood && allowArms)
        { 
            UpdataArmsSwing(LArm, movement, isBackOff, standFlag, isL);
            UpdataArmsSwing(RArm, movement, isBackOff, standFlag, isL);
        }
        //
        return lerpPos + new Vector3(0, stood ? 0 : GetY(pa, t, movement), 0);
    }

    private void UpdateDestination(LegPA pa,float bodyDot, Vector3 stepDistance, Vector3 anotherDestinPos, bool onlyChangeDeston = false)
    {  
        pa.goTo.position = hip.position + (stepDistance);
        RaycastHit hit;
        int missTimes = 0;
        Vector3 rayPoint = hip.position + (stepDistance);
        if (pa.goTo) pa.goTo.position = rayPoint;
        do
        {
            if (missTimes == 20) Debug.Log("20次测距射线失败 角色在高处边缘?");
            if (Physics.Raycast(rayPoint, Vector3.down, out hit, rayDistance, GroundMask)) break;
            stepDistance *= 0.9f;
            rayPoint = hip.position + (stepDistance);
            if (pa.goTo) pa.goTo.position = rayPoint;
            missTimes++;
            if (stepDistance.magnitude < 0.1f)//角色悬空
            {
                rigidbody.AddForce(-3 * stepDistance, ForceMode.VelocityChange);
                Debug.Log("角色在高处边缘");
                return;
            }
        } while (missTimes<=20); 
        if (Vector3.Distance(pa.destinPoint, hit.point) >= updataStepDistance)//|| IsTooClose(LLeg,RLeg) *speedOffset.magnitude 
        { 
            if (IsSmallerMove(bodyDot)) stepDistance = stepDistance.normalized * 0.5f;//魔法数字
            rayPoint = pa.Thigh.position + (stepDistance);
            if (pa.goTo) pa.goTo.position = rayPoint;
            if (Physics.Raycast(rayPoint, Vector3.down, out hit, rayDistance, GroundMask))
            {
                if (!onlyChangeDeston)
                {
                    pa.destinPre = pa.destinPoint;
                    pa.destinFlushTime = Time.time;
                }
                Vector3 finalDestin = hit.point;
                Vector3 gap = hit.point - anotherDestinPos;
                for (int times = 1; (finalDestin - anotherDestinPos).magnitude < updataStepDistance;times++)
                {
                    rayPoint = pa.Thigh.position + stepDistance + (times * gap);
                    if (pa.goTo) pa.goTo.position = rayPoint;
                    if (Physics.Raycast(rayPoint, Vector3.down, out hit, rayDistance, GroundMask))
                    {
                        finalDestin = hit.point;
                    }
                    if (times > 20)
                    {
                        Debug.LogError("20次向外偏移射线也太近 怎么回事");
                        break;
                    }
                        //finalDestin += gap;
                }
                pa.destinPoint = finalDestin; 
                return;
            }
        }
    }
    private void UpdateDestinationForce(LegPA pa, bool onlyDestin = false)
    {
        Vector3 rayPoint = pa.Thigh.position;
        if(pa.goTo)pa.goTo.position = rayPoint;
        if (Physics.Raycast(rayPoint, Vector3.down, out RaycastHit hit, rayDistance, GroundMask))
        {
            if (Vector3.Distance(pa.destinPoint, hit.point) >= updataStepDistance * 0.5f)
            {
                if (!onlyDestin)
                {
                    pa.destinPre = pa.destinPoint;
                    pa.destinFlushTime = Time.time;
                }
                pa.destinPoint = hit.point;
            }
        }
    }
    //public static float WalkLift(float t, float rise = 0.30f, float hold = 0.62f)
    //{
    //    t = Mathf.Clamp01(t);
    //    if (t < rise) return S(t / rise);
    //    if (t > hold) return S(1f - (t - hold) / (1f - hold));
    //    return 1f;

    //    static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    //} 
    static float FootLift(float t, float peak = 0.4f, float risePow = 1.5f, float fallPow = 1.6f)
    {
        t = Mathf.Clamp01(t);
        const float k = Mathf.PI * 0.5f;

        if (t <= peak)
        {
            float u = t / peak;
            return Mathf.Pow(Mathf.Sin(u * k), risePow);
        }
        float v = (t - peak) / (1f - peak);
        return Mathf.Pow(Mathf.Cos(v * k), fallPow);
    }
    private float GetY(LegPA pa, float t,Movement movement)
    {
        Vector3 curPos = new(pa.currentPos.x, 0, pa.currentPos.z);
        Vector3 destinPos = new(pa.destinPoint.x, 0, pa.destinPoint.z);
        Vector3 destinPre = new(pa.destinPre.x, 0, pa.destinPre.z);
        float risePow = (curPos - destinPos).magnitude / (destinPre - destinPos).magnitude;
        if (float.IsNaN(risePow)) risePow = 0f;
        if (float.IsInfinity(risePow)) risePow = 1;
        risePow *= 0.5f;
        if (t > 1) return 0;
        float y = 0;
        y = FootLift(t, movement.peak, movement.risePow, movement.fallPow);
        //if (movement == Movement.walk) y = WalkLift(t, 0.4f, risePow);
        //if (movement == Movement.run) y = FootLift(t, 0.50f, 1.0f, 0.90f);
        if (float.IsNaN(y)) y = 0f;
        if (float.IsInfinity(y)) y = 0.5f; 
        return y;
    }
    private bool IsTooClose(Vector3 L, Vector3 R)
    {
        bool isTooClose = (L - R).magnitude < tooCloseDistance;
        return isTooClose;
    }
    private float GetLegOffBalance(LegPA pa)
    {
        return (pa.currentPos - pa.destinPoint).magnitude;
    }
    private bool IsSmallerMove(float bodyDot)
    {
        return bodyDot < dotThreshold;
    }
    private void HipFlow(float t,bool standFlag)
    {
        // ---- 骨盆上下浮动：随本步进度 t 单峰起伏，端点归零，绝对定位不累积 ----
        if (hip != null)
        {
            float s = Mathf.Sin(Mathf.PI * t);          // 0 → 1 → 0
            float bob = standFlag ? 0f : hipBob * s * s;  // 平方后端点斜率为 0，无顿挫
            Vector3 hipBase = hip.parent != null
                ? hip.parent.TransformPoint(hipBaseLocal)
                : hipBaseLocal;
            hip.position = Vector3.Lerp(hip.position, hipBase + Vector3.up * bob, 0.5f);//lerp平滑 不能用t
        }
    }
    private void BodyRotation( Movement movement,float forwardDot, bool standing)
    {
        if (root == null)
        {
            Debug.LogError("root未配置");
            return;
        }
        Quaternion localRot = root.transform.localRotation;
        Vector3 leanRot = Vector3.zero;
        if (standing)
        {
            leanRot.x = idleLeanOffset;
        }
        else
        {
            bool isBackOff = forwardDot <= 0;
            forwardDot = math.abs(forwardDot);
            leanRot.x = movement.bodyLean * (isBackOff ? -backLeanScale : 1);
            leanRot.y = movement.bodyLean * (isBackOff ? -backLeanScale : 1) * (1 - forwardDot);
        }  
        Quaternion lerpQua = Quaternion.Lerp(localRot, rootBaseLocal * Quaternion.Euler(leanRot),movement.bodyLeanSpeed* Time.deltaTime); 
        root.transform.localRotation = lerpQua;
    }
    private void ChestRotation(Movement movement,float t,bool isBackOff, bool standFlag,bool isL)
    {
        if(hip == null || chest == null)
        {
            Debug.LogError("hip和chest未配置");
            return;
        }
        //Vector3 torsoAxis = chest.position - hip.position;
        //if (torsoAxis.magnitude < 0.05f || torsoAxis.y <= 0)  
        //{
        //    Debug.LogError("hip chest过近");
        //    torsoAxis = Vector3.up;
        //} 
        //t = Mathf.Clamp01(t-0.5f);
        t = Mathf.Sin((t * 1.57f)) ; 
        Vector3 lean = Vector3.zero;
        lean.y = (standFlag ? 0 : movement.chestLean) * (isL ? -1 : 1);
        if (isBackOff) lean.y*= backLeanScale;
        Quaternion leanChestRot = Quaternion.Lerp(chest.localRotation, Quaternion.Euler(lean) * chestBaseLocal,movement.chestLeanSpeed*Time.deltaTime);
        chest.localRotation = leanChestRot;
    }
    private void UpdataArmsSwing(ArmPA arm, Movement movement, bool isBackOff, bool standFlag, bool isL)
    {
        if (arm == null || arm.UpperArm == null || arm.Tar == null) return;
        Vector3 ArcPos( bool isL)
        { 
            Vector3 p = movement.arcPos;
            p.x *= isL ? -1 : 1;                       // 左右镜像，正 = 外 
            return arm.UpperArm.position + ( transform.rotation) * p;//Quaternion.Inverse(rootBaseLocal) *
        }
        Quaternion ArcRotation( )
        { 
            return transform.rotation * Quaternion.Euler(movement.arcEuler);//Quaternion.Inverse(rootBaseLocal) * 
        }
        Vector3 center = ArcPos(isL);
        Quaternion rot = ArcRotation();
        //float half = movement.arcSweep * 0.5f * Mathf.Deg2Rad;
        //防止顺拐
        bool sameSide = (arm == LArm) == isL;
        float dir = sameSide ? -1 : 1;
        //float theta = standFlag ? 0f : half * dir;// * (1f - 2f * u);
        // 圆心指向圆弧上一点
        float s = dir ;
        s = Mathf.Clamp(s, -1f, 1f);
        if (standFlag) s = 0;
        if (isBackOff) s *= backLeanScale;
        //曲线公式 两端抬高、中间最低  前后位移，与弓高无关
        Vector3 local = new (0, movement.arcBow * s * s, movement.arcLength * s);

        //Vector3 local = new Vector3(0f, 1f - Mathf.Cos(theta), Mathf.Sin(theta))  * movement.arcRadius;
        arm.Tar.position = Vector3.Lerp(arm.Tar.position, center + rot * local, movement.armSwingSpeed * Time.deltaTime * (isBackOff ? backLeanScale : 1));
        Vector3 handDir = (arm.Hand.position - arm.LowerArm.position).normalized;
        Vector3 refZ = Vector3.ProjectOnPlane(arm.LowerArm.forward, handDir).normalized;
        arm.Tar.rotation =Quaternion.LookRotation (refZ, handDir);
        //arm.destinPre = arm.destinPoint;
        //arm.destinPoint = center + rot * local;
    }
}