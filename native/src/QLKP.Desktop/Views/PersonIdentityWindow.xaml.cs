using System;
using System.Windows;
using QLKP.Desktop.Core;

namespace QLKP.Desktop.Views {
 public partial class PersonIdentityWindow : Window {
  readonly Session session;
  readonly PersonRow person;
  public string MatchedPersonId {get;private set;}

  public PersonIdentityWindow(Session actor,string personId=null) {
   InitializeComponent();session=actor;
   if(personId==null) {
    if(!DataStore.Can(actor,"B02","identity_lookup"))throw new UnauthorizedAccessException();
    Title=Heading.Text="Tra cứu CCCD";
    SubmitButton.Content="Tra cứu";
   } else {
    if(!DataStore.Can(actor,"B02","update")||!DataStore.Can(actor,"B02","identity_write"))throw new UnauthorizedAccessException();
    person=DataStore.GetPerson(actor,personId);
    Title="Ghi CCCD";
    Heading.Text=person.FullName;
    Current.Text=DataStore.GetMaskedPersonIdentity(actor,person.Id);
    SubmitButton.Content="Lưu CCCD";
   }
   Loaded+=(s,e)=>Number.Focus();
  }

  void Submit_Click(object sender,RoutedEventArgs e) {
   string input=Number.Password;
   Number.Clear();Message.Text="";
   try {
    if(person==null) {
     MatchedPersonId=DataStore.FindPersonIdByIdentity(session,input);
     if(MatchedPersonId.Length==0){Message.Text="Không tìm thấy hồ sơ trong khu phố đang làm việc.";return;}
    } else {
     if(MessageBox.Show(this,"Ghi số vừa nhập cho hồ sơ của "+person.FullName+"?",
       "Xác nhận định danh",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
     DataStore.SavePersonIdentity(session,person.Id,person.Version,input);
    }
    DialogResult=true;
   }catch(InvalidOperationException ex){Message.Text=ex.Message;}
    catch(UnauthorizedAccessException){Message.Text="Không có quyền hoặc hồ sơ ngoài phạm vi.";}
    catch(Exception){Message.Text="Thao tác chưa hoàn tất. Mở lại hồ sơ rồi thử lại.";}
  }
 }
}
