/* CaptureManager::GetCaptureFromTZ(_Image_Info&) */

CaptureManager __thiscall
CaptureManager::GetCaptureFromTZ(CaptureManager *this,_Image_Info *param_1)

{
  int *piVar1;
  int iVar2;
  uint uVar3;
  CaptureManager CVar4;
  undefined8 local_dc;
  undefined8 uStack_d4;
  undefined4 local_cc;
  undefined4 uStack_c8;
  undefined4 local_c4;
  undefined4 uStack_c0;
  undefined4 uStack_bc;
  undefined1 auStack_b8 [40];
  undefined4 local_90;
  undefined4 uStack_8c;
  undefined1 *local_88;
  undefined1 *local_84;
  undefined1 auStack_68 [4];
  undefined8 local_64;
  undefined4 local_5c;
  uint local_54;
  uint uStack_50;
  CaptureManager *local_48;
  undefined4 local_44;
  uint local_40;
  CaptureManager *local_3c;
  undefined4 local_38;
  uint local_34;
  int local_2c;
  
  CVar4 = this[4];
  local_2c = __stack_chk_guard;
  if (CVar4 == (CaptureManager)0x0) {
    __dlog_print(0,6,&DAT_0001df34,"%s: %s(%d) > CA session is not opened yet\n",
                 "CaptureManager.cpp","GetCaptureFromTZ",0x102);
  }
  else {
    local_c4 = 0;
    local_dc = 0;
    uStack_d4 = 0;
    uStack_c0 = 0;
    uStack_bc = 0;
    memset(auStack_b8,0,0x50);
    local_cc = 0xffff;
    uStack_c8 = 0xffff;
    local_88 = ybuff;
    local_84 = cbuff;
    local_90 = 0x7e900;
    uStack_8c = 0x7e900;
    local_c4 = CONCAT31(local_c4._1_3_,1);
    piVar1 = (int *)IVideoCapture::getInstance();
    (**(code **)(*piVar1 + 0x34))(piVar1,1,0);
    piVar1 = (int *)IVideoCapture::getInstance();
    iVar2 = (**(code **)(*piVar1 + 0xc))(piVar1,&local_dc,auStack_b8);
    uVar3 = iVar2 + 4U & 0xfffffffb;
    if (uVar3 == 0) {
      memset(*(void **)(this + 0x30),0,*(size_t *)(this + 0x34));
      memset(*(void **)(this + 0x54),0,*(size_t *)(this + 0x58));
      local_38 = *(undefined4 *)(this + 0x58);
      local_44 = *(undefined4 *)(this + 0x34);
      local_64 = 0x3c00e0e0201;
      local_5c = 0x21c;
      local_48 = this + 0x30;
      local_3c = this + 0x54;
      local_40 = uVar3;
      local_34 = uVar3;
      iVar2 = TEEC_InvokeCommand(this + 0x2c,2,auStack_68,0);
      piVar1 = (int *)IVideoCapture::getInstance();
      (**(code **)(*piVar1 + 0x38))(piVar1,1,0);
      if (((iVar2 == 0) && (0x3bf < local_54)) && (0x21b < uStack_50)) {
        memcpy(*(void **)param_1,*(void **)(this + 0x30),0x7e900);
        memcpy(*(void **)(param_1 + 4),*(void **)(this + 0x54),0x7e900);
        *(ulonglong *)(param_1 + 8) = CONCAT44(uStack_50,local_54);
        *(undefined4 *)(param_1 + 0x1c) = 0;
        __dlog_print(0,3,&DAT_0001df34,"%s: %s(%d) > outRawImage.width = [%d]\n",
                     "CaptureManager.cpp","GetCaptureFromTZ",0x140,local_54);
        __dlog_print(0,3,&DAT_0001df34,"%s: %s(%d) > outRawImage.height = [%d]\n",
                     "CaptureManager.cpp","GetCaptureFromTZ",0x141,*(undefined4 *)(param_1 + 0xc));
        __dlog_print(0,3,&DAT_0001df34,"%s: %s(%d) > GetCaptureFromTZ() Success\n",
                     "CaptureManager.cpp","GetCaptureFromTZ",0x143);
      }
      else {
        CVar4 = (CaptureManager)0x0;
        __dlog_print(0,6,&DAT_0001df34,
                     "%s: %s(%d) > TEEC_InvokeCommand() failed; result = %d, WXH = %dX%d\n",
                     "CaptureManager.cpp","GetCaptureFromTZ",0x136,iVar2,local_54,uStack_50);
      }
    }
    else {
      piVar1 = (int *)IVideoCapture::getInstance();
      CVar4 = (CaptureManager)0x0;
      (**(code **)(*piVar1 + 0x38))(piVar1,1);
      __dlog_print(0,6,&DAT_0001df34,"%s: %s(%d) > getVideoMainYUV Capture Failed ret %d\n",
                   "CaptureManager.cpp","GetCaptureFromTZ",0x11a,iVar2);
    }
  }
  if (__stack_chk_guard == local_2c) {
    return CVar4;
  }
                    /* WARNING: Subroutine does not return */
  __stack_chk_fail();
}