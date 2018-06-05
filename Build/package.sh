SOLUTION_DIR=$1
PROJECTS=$(echo $2 | tr ";" "\n")
PUBLISH_DIRECTORY=$3
PROJECT_OUTPUT_FOLDER=$4

if [ -z "$5" ]
then
    PACKAGE_NAME=`basename $SOLUTION_DIR`
else
    PACKAGE_NAME=$5
fi


for PROJ in $PROJECTS
do
	mkdir $PUBLISH_DIRECTORY/$PROJ
	cp -R -f $SOLUTION_DIR/$PROJ/$PROJECT_OUTPUT_FOLDER/* $PUBLISH_DIRECTORY/$PROJ
done

(cd $PUBLISH_DIRECTORY && tar -zcvf $PUBLISH_DIRECTORY/$PACKAGE_NAME.tar.gz *)

if `command -v zip >/dev/null`
then
	find $PUBLISH_DIRECTORY ! -name "*.tar.gz" | zip -r $PUBLISH_DIRECTORY/$PACKAGE_NAME.zip -@
else
	echo "Zip is not installed. Unable to create zip archive."
fi